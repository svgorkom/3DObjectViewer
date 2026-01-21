using System.Windows.Media.Media3D;

namespace _3DObjectViewer.Core.Physics.Abstractions;

/// <summary>
/// Defines the contract for a physics simulation engine.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction allows switching between different physics implementations:
/// <list type="bullet">
///   <item><see cref="CustomPhysicsEngine"/> - Simple custom implementation</item>
///   <item><see cref="BepuPhysicsEngine"/> - Professional physics using BEPUphysics2</item>
/// </list>
/// </para>
/// </remarks>
public interface IPhysicsEngine : IDisposable
{
    /// <summary>
    /// Gets the name of this physics engine implementation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the number of physics bodies in the simulation.
    /// </summary>
    int BodyCount { get; }

    /// <summary>
    /// Gets the number of active (non-sleeping) bodies.
    /// </summary>
    int ActiveBodyCount { get; }

    /// <summary>
    /// Gets or sets the gravity acceleration (positive value, applied downward).
    /// </summary>
    double Gravity { get; set; }

    /// <summary>
    /// Gets or sets the ground plane Z position.
    /// </summary>
    double GroundLevel { get; set; }

    /// <summary>
    /// Gets or sets the time scale for simulation (1.0 = real time).
    /// </summary>
    double TimeScale { get; set; }

    /// <summary>
    /// Gets whether the simulation is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Occurs after physics bodies have been updated.
    /// </summary>
    event Action<IReadOnlyList<RigidBody>>? BodiesUpdated;

    /// <summary>
    /// Occurs when a body collides with the ground.
    /// </summary>
    event Action<RigidBody>? GroundCollision;

    /// <summary>
    /// Starts the physics simulation.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the physics simulation.
    /// </summary>
    void Stop();

    /// <summary>
    /// Toggles the physics simulation on/off.
    /// </summary>
    void Toggle();

    /// <summary>
    /// Adds a rigid body to the simulation.
    /// </summary>
    void AddBody(RigidBody body);

    /// <summary>
    /// Removes a rigid body from the simulation.
    /// </summary>
    void RemoveBody(RigidBody body);

    /// <summary>
    /// Removes a rigid body by its unique identifier.
    /// </summary>
    bool RemoveBodyById(Guid id);

    /// <summary>
    /// Gets a rigid body by its unique identifier.
    /// </summary>
    RigidBody? GetBodyById(Guid id);

    /// <summary>
    /// Clears all bodies from the simulation.
    /// </summary>
    void Clear();

    /// <summary>
    /// Wakes all bodies (removes them from sleep state).
    /// </summary>
    void WakeAllBodies();

    /// <summary>
    /// Applies an impulse to a body.
    /// </summary>
    void ApplyImpulse(RigidBody body, Vector3D impulse);

    /// <summary>
    /// Configures bucket boundaries for wall collisions.
    /// </summary>
    void SetBucketBounds(double minX, double maxX, double minY, double maxY, double height);

    /// <summary>
    /// Disables bucket wall collisions.
    /// </summary>
    void DisableBucket();
}
