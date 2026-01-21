using System.Windows.Media.Media3D;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Represents a rigid body with physics properties for simulation.
/// </summary>
/// <remarks>
/// <para>
/// A rigid body is the physics representation of a 3D object containing
/// position, velocity, mass, friction, bounciness, and collision bounds.
/// </para>
/// <para>
/// <b>Coordinate system:</b> X/Y horizontal, Z vertical (up). Gravity pulls in -Z direction.
/// </para>
/// </remarks>
public class RigidBody
{
    /// <summary>
    /// Gets the unique identifier for this body.
    /// </summary>
    public Guid Id { get; }

    #region Transform State

    /// <summary>
    /// Gets or sets the position of the object center in world coordinates.
    /// </summary>
    public Point3D Position { get; set; }

    /// <summary>
    /// Gets or sets the velocity in units per second.
    /// </summary>
    public Vector3D Velocity { get; set; }

    /// <summary>
    /// Gets or sets the angular velocity in radians per second.
    /// </summary>
    public Vector3D AngularVelocity { get; set; }

    /// <summary>
    /// Gets or sets the orientation as a quaternion.
    /// </summary>
    public Quaternion Orientation { get; set; }

    #endregion

    #region Physical Properties

    /// <summary>
    /// Gets or sets the mass in kilograms.
    /// </summary>
    public double Mass { get; set; } = PhysicsConstants.DefaultMass;

    /// <summary>
    /// Gets or sets the bounciness (coefficient of restitution, 0-1).
    /// </summary>
    public double Bounciness { get; set; } = PhysicsConstants.DefaultBounciness;

    /// <summary>
    /// Gets or sets the friction coefficient (0-1).
    /// </summary>
    public double Friction { get; set; } = PhysicsConstants.DefaultFriction;

    /// <summary>
    /// Gets or sets the drag coefficient for air resistance.
    /// </summary>
    public double Drag { get; set; } = PhysicsConstants.DefaultDrag;

    #endregion

    #region Collision Bounds

    /// <summary>
    /// Gets or sets the bounding radius for collision detection.
    /// </summary>
    public double BoundingRadius { get; set; } = PhysicsConstants.DefaultBoundingRadius;

    /// <summary>
    /// Gets or sets the height for ground collision.
    /// </summary>
    public double Height { get; set; } = PhysicsConstants.DefaultHeight;

    #endregion

    #region Simulation State

    /// <summary>
    /// Gets or sets whether the object is at rest (sleeping).
    /// </summary>
    public bool IsAtRest { get; set; }

    /// <summary>
    /// Gets or sets whether this object is kinematic (immovable).
    /// </summary>
    public bool IsKinematic { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the bottom Z position.
    /// </summary>
    public double BottomZ => Position.Z - Height * 0.5;

    /// <summary>
    /// Gets whether the object is on or near the ground.
    /// </summary>
    public bool IsGrounded => BottomZ <= PhysicsConstants.GroundedThreshold;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="RigidBody"/> class.
    /// </summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="position">Initial center position.</param>
    /// <param name="boundingRadius">Collision sphere radius.</param>
    /// <param name="height">Object height for ground detection.</param>
    public RigidBody(Guid id, Point3D position, double boundingRadius, double height)
    {
        Id = id;
        Position = position;
        Velocity = default;
        AngularVelocity = default;
        Orientation = Quaternion.Identity;
        BoundingRadius = boundingRadius;
        Height = Math.Max(height, PhysicsConstants.MinHeight);
    }
}
