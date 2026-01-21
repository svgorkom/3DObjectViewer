using System.Numerics;
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
/// Physics engine implementation using BEPUphysics2.
/// </summary>
/// <remarks>
/// <para>
/// BEPUphysics2 provides professional-grade physics simulation with:
/// </para>
/// <list type="bullet">
///   <item>Stable constraint solving for accurate stacking</item>
///   <item>Continuous collision detection to prevent tunneling</item>
///   <item>SIMD-optimized performance</item>
///   <item>Built-in sleep system for inactive bodies</item>
/// </list>
/// </remarks>
public sealed class BepuPhysicsEngine : IPhysicsEngine
{
    #region Fields

    private readonly BufferPool _bufferPool;
    private readonly ThreadDispatcher _threadDispatcher;
    private readonly DispatcherTimer _timer;
    
    // Shared gravity reference for dynamic updates
    private readonly PoseIntegratorCallbacks.GravityReference _gravityRef;
    
    private Simulation _simulation = null!;
    private DateTime _lastUpdate;
    private bool _isRunning;
    private bool _disposed;

    // Body tracking
    private readonly Dictionary<Guid, RigidBody> _rigidBodies = [];
    private readonly Dictionary<Guid, BodyHandle> _bodyHandles = [];
    private readonly Dictionary<BodyHandle, Guid> _handleToId = [];
    private readonly List<RigidBody> _updatedBodies = [];

    // Boundary configuration
    private readonly List<StaticHandle> _boundaryStatics = [];
    private float _groundLevel;
    private float _bucketMinX, _bucketMaxX, _bucketMinY, _bucketMaxY, _bucketHeight;
    private bool _bucketEnabled;

    #endregion

    #region Properties

    /// <inheritdoc/>
    public string Name => "BEPUphysics2";

    /// <inheritdoc/>
    public int BodyCount => _rigidBodies.Count;

    /// <inheritdoc/>
    public int ActiveBodyCount => _simulation?.Bodies.ActiveSet.Count ?? 0;

    /// <inheritdoc/>
    public double Gravity
    {
        get => -_gravityRef.GravityZ;
        set => _gravityRef.GravityZ = -(float)value;
    }

    /// <inheritdoc/>
    public double GroundLevel
    {
        get => _groundLevel;
        set
        {
            _groundLevel = (float)value;
            RebuildBoundaries();
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
    /// Initializes a new instance of the <see cref="BepuPhysicsEngine"/> class.
    /// </summary>
    public BepuPhysicsEngine()
    {
        _bufferPool = new BufferPool();
        _threadDispatcher = new ThreadDispatcher(Environment.ProcessorCount);
        
        // Create shared gravity reference (negative because gravity pulls down in -Z)
        _gravityRef = new PoseIntegratorCallbacks.GravityReference(-(float)PhysicsConstants.DefaultGravity);

        InitializeSimulation();

        _timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(PhysicsConstants.FrameIntervalMs)
        };
        _timer.Tick += OnTimerTick;
        _lastUpdate = DateTime.Now;
    }

    private void InitializeSimulation()
    {
        var narrowPhaseCallbacks = NarrowPhaseCallbacks.CreateDefault();
        var poseIntegratorCallbacks = PoseIntegratorCallbacks.CreateWithReference(_gravityRef);
        var solveDescription = new SolveDescription(8, 1);

        _simulation = Simulation.Create(
            _bufferPool,
            narrowPhaseCallbacks,
            poseIntegratorCallbacks,
            solveDescription);

        RebuildBoundaries();
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Stop();
        Clear();

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
        if (_rigidBodies.ContainsKey(body.Id)) return;

        var sphere = new Sphere((float)body.BoundingRadius);
        var shapeIndex = _simulation.Shapes.Add(sphere);
        var inertia = sphere.ComputeInertia((float)body.Mass);

        var position = new Vector3(
            (float)body.Position.X,
            (float)body.Position.Y,
            (float)body.Position.Z);

        var velocity = new Vector3(
            (float)body.Velocity.X,
            (float)body.Velocity.Y,
            (float)body.Velocity.Z);

        var bodyDescription = BodyDescription.CreateDynamic(
            new RigidPose(position),
            new BodyVelocity(velocity),
            inertia,
            new CollidableDescription(shapeIndex, 0.1f),
            new BodyActivityDescription(0.01f));

        var handle = _simulation.Bodies.Add(bodyDescription);

        _rigidBodies[body.Id] = body;
        _bodyHandles[body.Id] = handle;
        _handleToId[handle] = body.Id;
    }

    /// <inheritdoc/>
    public void RemoveBody(RigidBody body) => RemoveBodyById(body.Id);

    /// <inheritdoc/>
    public bool RemoveBodyById(Guid id)
    {
        if (!_bodyHandles.TryGetValue(id, out var handle))
            return false;

        if (_simulation.Bodies.BodyExists(handle))
            _simulation.Bodies.Remove(handle);

        _bodyHandles.Remove(id);
        _handleToId.Remove(handle);
        _rigidBodies.Remove(id);

        return true;
    }

    /// <inheritdoc/>
    public RigidBody? GetBodyById(Guid id) => _rigidBodies.GetValueOrDefault(id);

    /// <inheritdoc/>
    public void Clear()
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
        if (_isRunning) return;

        _lastUpdate = DateTime.Now;
        _isRunning = true;
        _timer.Start();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _isRunning = false;
        _timer.Stop();
    }

    /// <inheritdoc/>
    public void Toggle()
    {
        if (_isRunning) Stop();
        else Start();
    }

    /// <inheritdoc/>
    public void WakeAllBodies()
    {
        foreach (var handle in _bodyHandles.Values)
        {
            if (_simulation.Bodies.BodyExists(handle))
                _simulation.Awakener.AwakenBody(handle);
        }
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
        RebuildBoundaries();
    }

    /// <inheritdoc/>
    public void DisableBucket()
    {
        _bucketEnabled = false;
        RebuildBoundaries();
    }

    private void RebuildBoundaries()
    {
        if (_simulation is null) return;

        // Remove existing boundary statics
        foreach (var handle in _boundaryStatics)
        {
            if (_simulation.Statics.StaticExists(handle))
                _simulation.Statics.Remove(handle);
        }
        _boundaryStatics.Clear();

        // Create ground plane
        AddStaticBox(new Vector3(0, 0, _groundLevel - 0.5f), 100, 100, 1);

        if (_bucketEnabled)
        {
            CreateBucketWalls();
        }
    }

    private void CreateBucketWalls()
    {
        const float wallThickness = 1f;
        float bucketWidth = _bucketMaxX - _bucketMinX;
        float bucketDepth = _bucketMaxY - _bucketMinY;
        float centerX = (_bucketMinX + _bucketMaxX) / 2;
        float centerY = (_bucketMinY + _bucketMaxY) / 2;
        float halfHeight = _bucketHeight / 2;

        // Left wall (-X)
        AddStaticBox(
            new Vector3(_bucketMinX - wallThickness / 2, centerY, halfHeight),
            wallThickness, bucketDepth, _bucketHeight);

        // Right wall (+X)
        AddStaticBox(
            new Vector3(_bucketMaxX + wallThickness / 2, centerY, halfHeight),
            wallThickness, bucketDepth, _bucketHeight);

        // Front wall (-Y)
        AddStaticBox(
            new Vector3(centerX, _bucketMinY - wallThickness / 2, halfHeight),
            bucketWidth + wallThickness * 2, wallThickness, _bucketHeight);

        // Back wall (+Y)
        AddStaticBox(
            new Vector3(centerX, _bucketMaxY + wallThickness / 2, halfHeight),
            bucketWidth + wallThickness * 2, wallThickness, _bucketHeight);
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
        if (!_bodyHandles.TryGetValue(body.Id, out var handle)) return;

        var bodyRef = _simulation.Bodies.GetBodyReference(handle);
        bodyRef.ApplyLinearImpulse(new Vector3((float)impulse.X, (float)impulse.Y, (float)impulse.Z));
        _simulation.Awakener.AwakenBody(handle);
    }

    #endregion

    #region Simulation Update

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        var now = DateTime.Now;
        var deltaTime = (now - _lastUpdate).TotalSeconds * TimeScale;
        _lastUpdate = now;

        if (deltaTime > PhysicsConstants.MaxDeltaTime)
            deltaTime = PhysicsConstants.MaxDeltaTime;

        _simulation.Timestep((float)deltaTime, _threadDispatcher);
        SyncBodiesToRigidBodies();
    }

    private void SyncBodiesToRigidBodies()
    {
        _updatedBodies.Clear();

        foreach (var (id, handle) in _bodyHandles)
        {
            if (!_simulation.Bodies.BodyExists(handle)) continue;

            var bodyRef = _simulation.Bodies.GetBodyReference(handle);
            var rigidBody = _rigidBodies[id];

            // Sync position
            var pos = bodyRef.Pose.Position;
            rigidBody.Position = new Point3D(pos.X, pos.Y, pos.Z);

            // Sync orientation
            var orientation = bodyRef.Pose.Orientation;
            rigidBody.Orientation = new System.Windows.Media.Media3D.Quaternion(
                orientation.X, orientation.Y, orientation.Z, orientation.W);

            // Sync velocities
            var vel = bodyRef.Velocity.Linear;
            rigidBody.Velocity = new Vector3D(vel.X, vel.Y, vel.Z);

            var angVel = bodyRef.Velocity.Angular;
            rigidBody.AngularVelocity = new Vector3D(angVel.X, angVel.Y, angVel.Z);

            // Sync sleep state
            rigidBody.IsAtRest = !bodyRef.Awake;

            // Check for ground collision
            if (rigidBody.BottomZ <= _groundLevel + PhysicsConstants.GroundedThreshold)
            {
                if (Math.Abs(vel.Z) <= PhysicsConstants.RestThreshold)
                    GroundCollision?.Invoke(rigidBody);
            }

            _updatedBodies.Add(rigidBody);
        }

        if (_updatedBodies.Count > 0)
            BodiesUpdated?.Invoke(_updatedBodies);
    }

    #endregion
}
