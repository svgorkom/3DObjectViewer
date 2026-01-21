using System.Numerics;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using _3DObjectViewer.Core.Physics.Abstractions;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Custom physics engine implementation using simple impulse-based dynamics.
/// </summary>
/// <remarks>
/// <para>
/// This engine orchestrates physics simulation using specialized components:
/// <list type="bullet">
///   <item><see cref="SpatialGrid"/> - Broad-phase collision detection optimization</item>
///   <item><see cref="CollisionResolver"/> - Collision response calculations</item>
///   <item><see cref="ForceIntegrator"/> - Gravity, drag, and position updates</item>
///   <item><see cref="BoundaryCollisionHandler"/> - Ground and wall collisions</item>
/// </list>
/// </para>
/// <para>
/// <b>Note:</b> For more robust physics (better stacking, continuous collision detection),
/// consider using <see cref="BepuPhysicsEngine"/> instead.
/// </para>
/// </remarks>
public class CustomPhysicsEngine : IPhysicsEngine
{
    #region Fields

    private readonly List<RigidBody> _bodies = [];
    private readonly Dictionary<Guid, RigidBody> _bodiesById = [];
    private readonly DispatcherTimer _timer;
    private DateTime _lastUpdate;
    private bool _isRunning;

    // Specialized components
    private readonly SpatialGrid _spatialGrid = new();
    private readonly BoundaryCollisionHandler _boundaryHandler = new();

    // Reusable collections to avoid allocations
    private readonly List<RigidBody> _activeBodies = [];
    private readonly List<RigidBody> _updatedBodies = [];
    
    // Track which bodies are supported (resting on ground or other objects)
    private readonly HashSet<Guid> _supportedBodies = [];

    #endregion

    #region Properties

    /// <inheritdoc/>
    public string Name => "Custom Physics Engine";

    /// <inheritdoc/>
    public int BodyCount => _bodies.Count;

    /// <inheritdoc/>
    public int ActiveBodyCount => _activeBodies.Count;

    /// <inheritdoc/>
    public double Gravity { get; set; } = PhysicsConstants.DefaultGravity;

    /// <inheritdoc/>
    public double GroundLevel
    {
        get => _boundaryHandler.GroundLevel;
        set => _boundaryHandler.GroundLevel = (float)value;
    }

    /// <inheritdoc/>
    public double TimeScale { get; set; } = PhysicsConstants.DefaultTimeScale;

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets whether bucket wall collisions are enabled.
    /// </summary>
    public bool BucketEnabled => _boundaryHandler.BucketEnabled;

    #endregion

    #region Events

    /// <inheritdoc/>
    public event Action<IReadOnlyList<RigidBody>>? BodiesUpdated;

    /// <inheritdoc/>
    public event Action<RigidBody>? GroundCollision;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPhysicsEngine"/> class.
    /// </summary>
    public CustomPhysicsEngine()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(PhysicsConstants.FrameIntervalMs)
        };
        _timer.Tick += OnTimerTick;
        _lastUpdate = DateTime.Now;
    }

    #endregion

    #region IDisposable

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
        Clear();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Body Management

    /// <inheritdoc/>
    public void AddBody(RigidBody body)
    {
        if (!_bodiesById.ContainsKey(body.Id))
        {
            _bodies.Add(body);
            _bodiesById[body.Id] = body;
        }
    }

    /// <inheritdoc/>
    public void RemoveBody(RigidBody body)
    {
        _bodies.Remove(body);
        _bodiesById.Remove(body.Id);
        _supportedBodies.Remove(body.Id);
    }

    /// <inheritdoc/>
    public bool RemoveBodyById(Guid id)
    {
        if (_bodiesById.TryGetValue(id, out var body))
        {
            _bodies.Remove(body);
            _bodiesById.Remove(id);
            _supportedBodies.Remove(id);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public RigidBody? GetBodyById(Guid id)
    {
        return _bodiesById.GetValueOrDefault(id);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _bodies.Clear();
        _bodiesById.Clear();
        _supportedBodies.Clear();
    }

    #endregion

    #region Simulation Control

    /// <inheritdoc/>
    public void Start()
    {
        if (!_isRunning)
        {
            _lastUpdate = DateTime.Now;
            _isRunning = true;
            _timer.Start();
        }
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
        if (_isRunning)
            Stop();
        else
            Start();
    }

    /// <inheritdoc/>
    public void WakeAllBodies()
    {
        foreach (var body in _bodies)
        {
            body.IsAtRest = false;
        }
        _supportedBodies.Clear();
    }

    #endregion

    #region Boundary Configuration

    /// <inheritdoc/>
    public void SetBucketBounds(double minX, double maxX, double minY, double maxY, double height)
    {
        _boundaryHandler.SetBucketBounds(minX, maxX, minY, maxY, height);
    }

    /// <inheritdoc/>
    public void DisableBucket()
    {
        _boundaryHandler.DisableBucket();
    }

    #endregion

    #region Force Application

    /// <inheritdoc/>
    public void ApplyImpulse(RigidBody body, Vector3D impulse)
    {
        var velocity = body.Velocity.ToSimdVector3();
        var impulseSimd = impulse.ToSimdVector3();
        float inverseMass = 1.0f / (float)body.Mass;

        velocity = ForceIntegrator.ApplyImpulse(velocity, impulseSimd, inverseMass);
        body.Velocity = velocity.ToVector3D();
        body.IsAtRest = false;
        _supportedBodies.Remove(body.Id);
    }

    /// <summary>
    /// Drops a body from its current position (resets velocity).
    /// </summary>
    public void DropBody(RigidBody body)
    {
        body.Velocity = new Vector3D(0, 0, 0);
        body.IsAtRest = false;
        _supportedBodies.Remove(body.Id);
    }

    #endregion

    #region Simulation Update

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastUpdate).TotalSeconds * TimeScale;
        _lastUpdate = now;

        // Cap delta time to prevent physics explosions
        if (deltaTime > PhysicsConstants.MaxDeltaTime)
        {
            deltaTime = PhysicsConstants.MaxDeltaTime;
        }

        Update((float)deltaTime);
    }

    /// <summary>
    /// Updates the physics simulation by one time step.
    /// </summary>
    private void Update(float deltaTime)
    {
        // Clear support tracking for this frame
        _supportedBodies.Clear();
        
        CollectActiveBodies();

        if (_activeBodies.Count == 0)
        {
            return;
        }

        ApplyForcesToActiveBodies(deltaTime);
        ResolveObjectCollisions();
        UpdatePositionsAndBoundaries(deltaTime);
        CheckRestStates();
        NotifyUpdates();
    }

    /// <summary>
    /// Collects bodies that need physics updates.
    /// </summary>
    private void CollectActiveBodies()
    {
        _activeBodies.Clear();
        _updatedBodies.Clear();

        foreach (var body in _bodies)
        {
            if (!body.IsKinematic && !body.IsAtRest)
            {
                _activeBodies.Add(body);
            }
        }
    }

    /// <summary>
    /// Applies gravity and drag to all active bodies.
    /// </summary>
    private void ApplyForcesToActiveBodies(float deltaTime)
    {
        float gravity = (float)Gravity;

        foreach (var body in _activeBodies)
        {
            var velocity = body.Velocity.ToSimdVector3();
            velocity = ForceIntegrator.ApplyForces(velocity, gravity, (float)body.Drag, deltaTime);
            body.Velocity = velocity.ToVector3D();
        }
    }

    /// <summary>
    /// Resolves object-to-object collisions.
    /// </summary>
    private void ResolveObjectCollisions()
    {
        if (_bodies.Count < 2)
        {
            return;
        }

        // Multiple iterations for stacking and complex overlaps
        for (int i = 0; i < PhysicsConstants.CollisionIterations; i++)
        {
            if (_bodies.Count < PhysicsConstants.BruteForceThreshold)
            {
                ResolveCollisionsBruteForce();
            }
            else
            {
                ResolveCollisionsWithSpatialGrid();
            }
        }
    }

    /// <summary>
    /// Brute force collision resolution for small body counts.
    /// </summary>
    private void ResolveCollisionsBruteForce()
    {
        for (int i = 0; i < _bodies.Count; i++)
        {
            var bodyA = _bodies[i];
            if (bodyA.IsKinematic)
            {
                continue;
            }

            for (int j = i + 1; j < _bodies.Count; j++)
            {
                var bodyB = _bodies[j];
                if (bodyB.IsKinematic)
                {
                    continue;
                }

                // Allow collision check even if both at rest - they might need to be woken
                ResolveCollisionBetween(bodyA, bodyB);
            }
        }
    }

    /// <summary>
    /// Spatial grid collision resolution for large body counts.
    /// </summary>
    private void ResolveCollisionsWithSpatialGrid()
    {
        _spatialGrid.Build(_bodies);
        _spatialGrid.ForEachPotentialPair(ResolveCollisionBetween);
    }

    /// <summary>
    /// Resolves collision between two specific bodies.
    /// </summary>
    private void ResolveCollisionBetween(RigidBody bodyA, RigidBody bodyB)
    {
        var posA = bodyA.Position.ToSimdVector3();
        var posB = bodyB.Position.ToSimdVector3();
        var velA = bodyA.Velocity.ToSimdVector3();
        var velB = bodyB.Velocity.ToSimdVector3();

        bool collided = CollisionResolver.Resolve(
            ref posA, ref posB,
            ref velA, ref velB,
            (float)bodyA.BoundingRadius, (float)bodyB.BoundingRadius,
            (float)bodyA.Mass, (float)bodyB.Mass,
            (float)bodyA.Bounciness, (float)bodyB.Bounciness,
            (float)bodyA.Friction, (float)bodyB.Friction,
            bodyA.IsKinematic, bodyB.IsKinematic);

        if (collided)
        {
            if (!bodyA.IsKinematic)
            {
                bodyA.Position = posA.ToPoint3D();
                bodyA.Velocity = velA.ToVector3D();
                bodyA.IsAtRest = false;
            }

            if (!bodyB.IsKinematic)
            {
                bodyB.Position = posB.ToPoint3D();
                bodyB.Velocity = velB.ToVector3D();
                bodyB.IsAtRest = false;
            }
            
            // Track support relationships for stacking
            // The body that's higher is supported by the lower one
            if (posA.Z > posB.Z)
            {
                _supportedBodies.Add(bodyA.Id);
            }
            else
            {
                _supportedBodies.Add(bodyB.Id);
            }
        }
    }

    /// <summary>
    /// Updates positions and handles boundary collisions.
    /// </summary>
    private void UpdatePositionsAndBoundaries(float deltaTime)
    {
        foreach (var body in _activeBodies)
        {
            var position = body.Position.ToSimdVector3();
            var velocity = body.Velocity.ToSimdVector3();

            // Integrate position
            position = ForceIntegrator.IntegratePosition(position, velocity, deltaTime);

            // Handle boundary collisions
            bool groundCollision = _boundaryHandler.HandleBoundaryCollisions(
                ref position, ref velocity,
                (float)body.BoundingRadius,
                (float)body.Height,
                (float)body.Bounciness,
                (float)body.Friction);

            body.Position = position.ToPoint3D();
            body.Velocity = velocity.ToVector3D();

            if (groundCollision)
            {
                _supportedBodies.Add(body.Id);
                GroundCollision?.Invoke(body);
            }

            _updatedBodies.Add(body);
        }
    }

    /// <summary>
    /// Checks and updates rest states for all active bodies.
    /// </summary>
    private void CheckRestStates()
    {
        foreach (var body in _activeBodies)
        {
            // Body can only rest if it's supported (by ground or another object)
            if (!_supportedBodies.Contains(body.Id))
            {
                continue;
            }

            var velocity = body.Velocity.ToSimdVector3();
            if (CollisionDetector.IsBelowRestThreshold(velocity))
            {
                body.Velocity = new Vector3D(0, 0, 0);
                body.AngularVelocity = new Vector3D(0, 0, 0);
                body.IsAtRest = true;
            }
        }
    }

    /// <summary>
    /// Notifies listeners of updated bodies.
    /// </summary>
    private void NotifyUpdates()
    {
        if (_updatedBodies.Count > 0)
        {
            BodiesUpdated?.Invoke(_updatedBodies);
        }
    }

    #endregion
}
