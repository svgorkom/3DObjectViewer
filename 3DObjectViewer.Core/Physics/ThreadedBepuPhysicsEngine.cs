using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using _3DObjectViewer.Core.Physics.Abstractions;
using _3DObjectViewer.Core.Physics.Bepu;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Thread-safe physics engine using BEPUphysics2 with background processing.
/// </summary>
/// <remarks>
/// Physics simulation runs on a dedicated background thread, with results
/// marshalled to the UI thread for visual updates. This keeps the UI responsive
/// even with many physics bodies.
/// </remarks>
public sealed class ThreadedBepuPhysicsEngine : IPhysicsEngine
{
    #region Fields

    private readonly BufferPool _bufferPool;
    private readonly ThreadDispatcher _threadDispatcher;
    private readonly Thread _physicsThread;
    private readonly ManualResetEventSlim _stopEvent = new(false);
    private readonly Dispatcher _uiDispatcher;
    private readonly object _simulationLock = new();
    
    private Simulation _simulation = null!;
    private volatile bool _isRunning;
    private volatile bool _disposed;

    // Body tracking (protected by _simulationLock - only accessed from physics thread after init)
    private readonly Dictionary<Guid, RigidBody> _rigidBodies = [];
    private readonly Dictionary<Guid, BodyHandle> _bodyHandles = [];
    private readonly Dictionary<BodyHandle, Guid> _handleToId = [];
    
    // Pending operations queue (has its own lock)
    private readonly object _operationsLock = new();
    private readonly Queue<Action> _pendingOperations = new();
    
    // Results for UI thread (no lock needed - only accessed from UI thread)
    private readonly List<RigidBody> _updatedBodies = [];

    // Boundary configuration
    private readonly List<StaticHandle> _boundaryStatics = [];
    private volatile float _groundLevel;
    private float _bucketMinX, _bucketMaxX, _bucketMinY, _bucketMaxY, _bucketHeight;
    private volatile bool _bucketEnabled;
    private volatile bool _boundariesDirty;

    #endregion

    #region Nested Types

    /// <summary>
    /// Snapshot of body state for thread-safe transfer to UI.
    /// </summary>
    private readonly record struct BodyState(
        Guid Id,
        Vector3 Position,
        System.Numerics.Quaternion Orientation,
        Vector3 LinearVelocity,
        Vector3 AngularVelocity,
        bool IsAwake,
        bool IsGrounded,
        double Height);

    #endregion

    #region Properties

    /// <inheritdoc/>
    public string Name => "BEPUphysics2 (Threaded)";

    /// <inheritdoc/>
    public int BodyCount => _rigidBodies.Count;

    /// <inheritdoc/>
    public int ActiveBodyCount => _simulation?.Bodies.ActiveSet.Count ?? 0;

    /// <inheritdoc/>
    public double Gravity { get; set; } = PhysicsConstants.DefaultGravity;

    /// <inheritdoc/>
    public double GroundLevel
    {
        get => _groundLevel;
        set
        {
            _groundLevel = (float)value;
            _boundariesDirty = true;
        }
    }

    /// <inheritdoc/>
    public double TimeScale { get; set; } = PhysicsConstants.DefaultTimeScale;

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    #endregion

    #region Events

    /// <inheritdoc/>
    public event Action<IReadOnlyList<RigidBody>>? BodiesUpdated;

    /// <inheritdoc/>
    public event Action<RigidBody>? GroundCollision;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadedBepuPhysicsEngine"/> class.
    /// </summary>
    public ThreadedBepuPhysicsEngine()
    {
        _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _bufferPool = new BufferPool();
        _threadDispatcher = new ThreadDispatcher(Math.Max(1, Environment.ProcessorCount - 1));

        InitializeSimulation();

        _physicsThread = new Thread(PhysicsLoop)
        {
            Name = "BepuPhysics",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
    }

    private void InitializeSimulation()
    {
        var narrowPhaseCallbacks = NarrowPhaseCallbacks.CreateDefault();
        var poseIntegratorCallbacks = PoseIntegratorCallbacks.CreateDefault((float)Gravity);
        
        _simulation = Simulation.Create(
            _bufferPool,
            narrowPhaseCallbacks,
            poseIntegratorCallbacks,
            new SolveDescription(8, 1));

        RebuildBoundariesInternal();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _isRunning = false;
        _stopEvent.Set();
        
        if (_physicsThread.IsAlive)
            _physicsThread.Join(2000);
        
        _stopEvent.Dispose();
        
        ClearInternal();
        _simulation?.Dispose();
        _threadDispatcher?.Dispose();
        _bufferPool?.Clear();

        GC.SuppressFinalize(this);
    }

    #endregion

    #region Body Management

    /// <inheritdoc/>
    public void AddBody(RigidBody body)
    {
        // Capture body state for thread-safe transfer
        var id = body.Id;
        var pos = new Vector3((float)body.Position.X, (float)body.Position.Y, (float)body.Position.Z);
        var vel = new Vector3((float)body.Velocity.X, (float)body.Velocity.Y, (float)body.Velocity.Z);
        var radius = (float)body.BoundingRadius;
        var mass = (float)body.Mass;
        
        EnqueueOperation(() =>
        {
            if (_rigidBodies.ContainsKey(id)) return;

            var sphere = new Sphere(radius);
            var shapeIndex = _simulation.Shapes.Add(sphere);
            var inertia = sphere.ComputeInertia(mass);

            var bodyDescription = BodyDescription.CreateDynamic(
                new RigidPose(pos),
                new BodyVelocity(vel),
                inertia,
                new CollidableDescription(shapeIndex, 0.1f),
                new BodyActivityDescription(0.01f));

            var handle = _simulation.Bodies.Add(bodyDescription);

            _rigidBodies[id] = body;
            _bodyHandles[id] = handle;
            _handleToId[handle] = id;
        });
    }

    /// <inheritdoc/>
    public void RemoveBody(RigidBody body) => RemoveBodyById(body.Id);

    /// <inheritdoc/>
    public bool RemoveBodyById(Guid id)
    {
        // Queue the removal for the physics thread
        EnqueueOperation(() =>
        {
            if (!_bodyHandles.TryGetValue(id, out var handle))
                return;

            if (_simulation.Bodies.BodyExists(handle))
                _simulation.Bodies.Remove(handle);

            _bodyHandles.Remove(id);
            _handleToId.Remove(handle);
            _rigidBodies.Remove(id);
        });
        return true;
    }

    /// <inheritdoc/>
    public RigidBody? GetBodyById(Guid id)
    {
        return _rigidBodies.GetValueOrDefault(id);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        EnqueueOperation(ClearInternal);
    }

    private void ClearInternal()
    {
        foreach (var handle in _bodyHandles.Values)
        {
            if (_simulation.Bodies.BodyExists(handle))
                _simulation.Bodies.Remove(handle);
        }
        _bodyHandles.Clear();
        _handleToId.Clear();
        _rigidBodies.Clear();
    }

    #endregion

    #region Simulation Control

    /// <inheritdoc/>
    public void Start()
    {
        if (_isRunning || _disposed) return;
        _isRunning = true;
        _stopEvent.Reset();
        
        if (!_physicsThread.IsAlive)
            _physicsThread.Start();
    }

    /// <inheritdoc/>
    public void Stop() => _isRunning = false;

    /// <inheritdoc/>
    public void Toggle()
    {
        if (_isRunning) Stop();
        else Start();
    }

    /// <inheritdoc/>
    public void WakeAllBodies()
    {
        EnqueueOperation(() =>
        {
            foreach (var handle in _bodyHandles.Values)
            {
                if (_simulation.Bodies.BodyExists(handle))
                    _simulation.Awakener.AwakenBody(handle);
            }
        });
    }

    #endregion

    #region Boundary Configuration

    /// <inheritdoc/>
    public void SetBucketBounds(double minX, double maxX, double minY, double maxY, double height)
    {
        _bucketMinX = (float)Math.Min(minX, maxX);
        _bucketMaxX = (float)Math.Max(minX, maxX);
        _bucketMinY = (float)Math.Min(minY, maxY);
        _bucketMaxY = (float)Math.Max(minY, maxY);
        _bucketHeight = (float)height;
        _bucketEnabled = true;
        _boundariesDirty = true;
    }

    /// <inheritdoc/>
    public void DisableBucket()
    {
        _bucketEnabled = false;
        _boundariesDirty = true;
    }

    private void RebuildBoundariesInternal()
    {
        foreach (var handle in _boundaryStatics)
        {
            if (_simulation.Statics.StaticExists(handle))
                _simulation.Statics.Remove(handle);
        }
        _boundaryStatics.Clear();

        // Ground plane
        AddStaticBox(new Vector3(0, 0, _groundLevel - 0.5f), 100, 100, 1);

        if (_bucketEnabled)
        {
            const float wallThickness = 1f;
            float bucketWidth = _bucketMaxX - _bucketMinX;
            float bucketDepth = _bucketMaxY - _bucketMinY;
            float centerX = (_bucketMinX + _bucketMaxX) / 2;
            float centerY = (_bucketMinY + _bucketMaxY) / 2;
            float halfHeight = _bucketHeight / 2;

            AddStaticBox(new Vector3(_bucketMinX - wallThickness / 2, centerY, halfHeight),
                wallThickness, bucketDepth, _bucketHeight);
            AddStaticBox(new Vector3(_bucketMaxX + wallThickness / 2, centerY, halfHeight),
                wallThickness, bucketDepth, _bucketHeight);
            AddStaticBox(new Vector3(centerX, _bucketMinY - wallThickness / 2, halfHeight),
                bucketWidth + wallThickness * 2, wallThickness, _bucketHeight);
            AddStaticBox(new Vector3(centerX, _bucketMaxY + wallThickness / 2, halfHeight),
                bucketWidth + wallThickness * 2, wallThickness, _bucketHeight);
        }
    }

    private void AddStaticBox(Vector3 position, float width, float depth, float height)
    {
        var box = new Box(width, depth, height);
        var shapeIndex = _simulation.Shapes.Add(box);
        var handle = _simulation.Statics.Add(new StaticDescription(position, shapeIndex));
        _boundaryStatics.Add(handle);
    }

    #endregion

    #region Force Application

    /// <inheritdoc/>
    public void ApplyImpulse(RigidBody body, Vector3D impulse)
    {
        var bodyId = body.Id;
        var impulseVec = new Vector3((float)impulse.X, (float)impulse.Y, (float)impulse.Z);
        
        EnqueueOperation(() =>
        {
            if (_bodyHandles.TryGetValue(bodyId, out var handle) && 
                _simulation.Bodies.BodyExists(handle))
            {
                var bodyRef = _simulation.Bodies.GetBodyReference(handle);
                bodyRef.ApplyLinearImpulse(impulseVec);
                _simulation.Awakener.AwakenBody(handle);
            }
        });
    }

    #endregion

    #region Threading Helpers

    private void EnqueueOperation(Action operation)
    {
        lock (_operationsLock)
            _pendingOperations.Enqueue(operation);
    }

    private void ProcessPendingOperations()
    {
        while (true)
        {
            Action? operation;
            lock (_operationsLock)
            {
                if (_pendingOperations.Count == 0) break;
                operation = _pendingOperations.Dequeue();
            }
            
            try { operation?.Invoke(); }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Physics operation failed: {ex.Message}"); 
            }
        }
    }

    #endregion

    #region Physics Loop (Background Thread)

    private void PhysicsLoop()
    {
        var lastUpdate = DateTime.UtcNow;
        var targetInterval = TimeSpan.FromMilliseconds(PhysicsConstants.FrameIntervalMs);

        while (!_disposed)
        {
            if (!_isRunning)
            {
                if (_stopEvent.Wait(50)) break;
                lastUpdate = DateTime.UtcNow;
                continue;
            }

            var frameStart = DateTime.UtcNow;
            
            try
            {
                // Process pending operations (add/remove bodies, etc.)
                ProcessPendingOperations();

                // Rebuild boundaries if needed
                if (_boundariesDirty)
                {
                    _boundariesDirty = false;
                    RebuildBoundariesInternal();
                }

                // Calculate delta time
                var now = DateTime.UtcNow;
                var deltaTime = (float)((now - lastUpdate).TotalSeconds * TimeScale);
                lastUpdate = now;

                if (deltaTime > (float)PhysicsConstants.MaxDeltaTime)
                    deltaTime = (float)PhysicsConstants.MaxDeltaTime;

                if (deltaTime > 0.001f && _bodyHandles.Count > 0)
                {
                    // Run physics simulation
                    _simulation.Timestep(deltaTime, _threadDispatcher);
                    
                    // Collect results (snapshot of physics state)
                    var results = CollectBodyStates();
                    
                    if (results.Count > 0)
                    {
                        // Send to UI thread - don't wait for completion
                        _uiDispatcher.BeginInvoke(DispatcherPriority.Normal, () => ApplyResults(results));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Physics loop error: {ex.Message}");
            }

            // Sleep to maintain target frame rate
            var elapsed = DateTime.UtcNow - frameStart;
            var sleepTime = targetInterval - elapsed;
            if (sleepTime > TimeSpan.Zero)
            {
                Thread.Sleep(sleepTime);
            }
        }
    }

    private List<BodyState> CollectBodyStates()
    {
        var results = new List<BodyState>(_bodyHandles.Count);
        var groundLevel = _groundLevel;
        
        foreach (var (id, handle) in _bodyHandles)
        {
            if (!_simulation.Bodies.BodyExists(handle)) continue;

            var bodyRef = _simulation.Bodies.GetBodyReference(handle);
            var rigidBody = _rigidBodies[id];
            var height = rigidBody.Height;
            
            // Calculate grounded state using BEPU position, not RigidBody position
            bool isGrounded = (bodyRef.Pose.Position.Z - height * 0.5f) <= 
                groundLevel + PhysicsConstants.GroundedThreshold;

            results.Add(new BodyState(
                id,
                bodyRef.Pose.Position,
                bodyRef.Pose.Orientation,
                bodyRef.Velocity.Linear,
                bodyRef.Velocity.Angular,
                bodyRef.Awake,
                isGrounded,
                height));
        }
        
        return results;
    }

    private void ApplyResults(List<BodyState> results)
    {
        if (_disposed) return;

        _updatedBodies.Clear();

        foreach (var state in results)
        {
            if (!_rigidBodies.TryGetValue(state.Id, out var rigidBody))
                continue;

            // Update RigidBody properties (on UI thread - safe)
            rigidBody.Position = new Point3D(state.Position.X, state.Position.Y, state.Position.Z);
            rigidBody.Orientation = new System.Windows.Media.Media3D.Quaternion(
                state.Orientation.X, state.Orientation.Y, state.Orientation.Z, state.Orientation.W);
            rigidBody.Velocity = new Vector3D(state.LinearVelocity.X, state.LinearVelocity.Y, state.LinearVelocity.Z);
            rigidBody.AngularVelocity = new Vector3D(state.AngularVelocity.X, state.AngularVelocity.Y, state.AngularVelocity.Z);
            rigidBody.IsAtRest = !state.IsAwake;

            if (state.IsGrounded && Math.Abs(state.LinearVelocity.Z) <= PhysicsConstants.RestThreshold)
            {
                GroundCollision?.Invoke(rigidBody);
            }

            _updatedBodies.Add(rigidBody);
        }

        if (_updatedBodies.Count > 0)
        {
            BodiesUpdated?.Invoke(_updatedBodies);
        }
    }

    #endregion
}
