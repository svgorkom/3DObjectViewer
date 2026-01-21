using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Physics.Abstractions;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Physics engine facade that delegates to the configured physics engine implementation.
/// </summary>
/// <remarks>
/// <para>
/// By default, uses BEPUphysics2 for robust physics simulation.
/// To change the physics engine, use the constructor that accepts a <see cref="PhysicsEngineType"/>.
/// </para>
/// </remarks>
public class PhysicsEngine : IPhysicsEngine
{
    private readonly IPhysicsEngine _engine;

    /// <summary>
    /// Initializes a new instance using BEPUphysics2 by default.
    /// </summary>
    public PhysicsEngine() : this(PhysicsEngineType.Bepu)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified engine type.
    /// </summary>
    /// <param name="engineType">The type of physics engine to use.</param>
    public PhysicsEngine(PhysicsEngineType engineType)
    {
        _engine = PhysicsEngineFactory.Create(engineType);
        
        // Wire up events
        _engine.BodiesUpdated += bodies => BodiesUpdated?.Invoke(bodies);
        _engine.GroundCollision += body => GroundCollision?.Invoke(body);
    }

    /// <inheritdoc/>
    public string Name => _engine.Name;

    /// <inheritdoc/>
    public int BodyCount => _engine.BodyCount;

    /// <inheritdoc/>
    public int ActiveBodyCount => _engine.ActiveBodyCount;

    /// <inheritdoc/>
    public double Gravity
    {
        get => _engine.Gravity;
        set => _engine.Gravity = value;
    }

    /// <inheritdoc/>
    public double GroundLevel
    {
        get => _engine.GroundLevel;
        set => _engine.GroundLevel = value;
    }

    /// <inheritdoc/>
    public double TimeScale
    {
        get => _engine.TimeScale;
        set => _engine.TimeScale = value;
    }

    /// <inheritdoc/>
    public bool IsRunning => _engine.IsRunning;

    /// <inheritdoc/>
    public event Action<IReadOnlyList<RigidBody>>? BodiesUpdated;

    /// <inheritdoc/>
    public event Action<RigidBody>? GroundCollision;

    /// <inheritdoc/>
    public void Start() => _engine.Start();

    /// <inheritdoc/>
    public void Stop() => _engine.Stop();

    /// <inheritdoc/>
    public void Toggle() => _engine.Toggle();

    /// <inheritdoc/>
    public void AddBody(RigidBody body) => _engine.AddBody(body);

    /// <inheritdoc/>
    public void RemoveBody(RigidBody body) => _engine.RemoveBody(body);

    /// <inheritdoc/>
    public bool RemoveBodyById(Guid id) => _engine.RemoveBodyById(id);

    /// <inheritdoc/>
    public RigidBody? GetBodyById(Guid id) => _engine.GetBodyById(id);

    /// <inheritdoc/>
    public void Clear() => _engine.Clear();

    /// <inheritdoc/>
    public void WakeAllBodies() => _engine.WakeAllBodies();

    /// <inheritdoc/>
    public void ApplyImpulse(RigidBody body, Vector3D impulse) => _engine.ApplyImpulse(body, impulse);

    /// <inheritdoc/>
    public void SetBucketBounds(double minX, double maxX, double minY, double maxY, double height)
        => _engine.SetBucketBounds(minX, maxX, minY, maxY, height);

    /// <inheritdoc/>
    public void DisableBucket() => _engine.DisableBucket();

    /// <inheritdoc/>
    public void Dispose()
    {
        _engine.Dispose();
        GC.SuppressFinalize(this);
    }
}
