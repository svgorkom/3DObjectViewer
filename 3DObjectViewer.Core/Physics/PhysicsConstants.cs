namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Centralized physics simulation constants.
/// </summary>
/// <remarks>
/// All tunable physics parameters are defined here for easy adjustment
/// and consistent behavior across the physics system.
/// </remarks>
public static class PhysicsConstants
{
    #region Simulation Timing

    /// <summary>
    /// Target frames per second for physics updates.
    /// </summary>
    public const int TargetFps = 60;

    /// <summary>
    /// Target frame interval in milliseconds.
    /// </summary>
    public const double FrameIntervalMs = 1000.0 / TargetFps;

    /// <summary>
    /// Maximum delta time to prevent physics explosions.
    /// </summary>
    public const double MaxDeltaTime = 0.1;

    /// <summary>
    /// Number of collision resolution iterations per frame.
    /// </summary>
    /// <remarks>
    /// Multiple iterations help resolve stacking and complex overlaps.
    /// </remarks>
    public const int CollisionIterations = 3;

    #endregion

    #region Rest State Detection

    /// <summary>
    /// Minimum velocity threshold below which objects are considered at rest.
    /// </summary>
    public const float RestThreshold = 0.1f;

    /// <summary>
    /// Squared rest threshold for faster comparison without sqrt.
    /// </summary>
    public const float RestThresholdSquared = RestThreshold * RestThreshold;

    /// <summary>
    /// Minimum height above ground to be considered grounded.
    /// </summary>
    public const float GroundedThreshold = 0.05f;

    #endregion

    #region Collision Response

    /// <summary>
    /// Minimum distance to avoid division by zero in collision calculations.
    /// </summary>
    public const float MinDistance = 0.0001f;

    /// <summary>
    /// Separation factor for position correction after collision.
    /// </summary>
    /// <remarks>
    /// Slightly greater than 1.0 to add a small buffer preventing re-collision.
    /// </remarks>
    public const float SeparationFactor = 1.01f;

    /// <summary>
    /// Maximum impulse magnitude to prevent explosive velocities.
    /// </summary>
    public const float MaxImpulse = 100.0f;

    /// <summary>
    /// Damping factor applied during collisions to prevent perpetual bouncing.
    /// </summary>
    /// <remarks>
    /// Higher = less energy loss. 1.0 = perfectly elastic, 0.0 = no bounce.
    /// </remarks>
    public const float CollisionDamping = 0.85f;

    /// <summary>
    /// Damping factor for wall/boundary collisions.
    /// </summary>
    public const float WallDamping = 0.8f;

    /// <summary>
    /// Friction multiplier for tangential velocity during ground contact.
    /// </summary>
    public const float GroundFrictionMultiplier = 0.15f;

    /// <summary>
    /// Friction multiplier for strong ground friction (when stopping).
    /// </summary>
    public const float StrongGroundFrictionMultiplier = 0.3f;

    #endregion

    #region Spatial Partitioning

    /// <summary>
    /// Size of each cell in the spatial partitioning grid.
    /// </summary>
    public const double CellSize = 2.0;

    /// <summary>
    /// Inverse of cell size for faster coordinate calculations.
    /// </summary>
    public const double InverseCellSize = 1.0 / CellSize;

    /// <summary>
    /// Maximum number of checked collision pairs per frame.
    /// </summary>
    public const int MaxCheckedPairs = 10000;

    /// <summary>
    /// Threshold for switching from spatial grid to brute force collision detection.
    /// </summary>
    public const int BruteForceThreshold = 30;

    /// <summary>
    /// Maximum spatial grid size before trimming.
    /// </summary>
    public const int MaxGridCells = 500;

    /// <summary>
    /// Maximum pooled lists to keep.
    /// </summary>
    public const int MaxPooledLists = 100;

    /// <summary>
    /// Initial capacity for pooled body lists.
    /// </summary>
    public const int InitialListCapacity = 8;

    /// <summary>
    /// Number of lists to pre-allocate in the pool.
    /// </summary>
    public const int PreAllocatedLists = 50;

    #endregion

    #region Default Physics Properties

    /// <summary>
    /// Default gravity acceleration (Earth-like).
    /// </summary>
    public const double DefaultGravity = 9.81;

    /// <summary>
    /// Default time scale for simulation.
    /// </summary>
    public const double DefaultTimeScale = 1.0;

    /// <summary>
    /// Default mass for rigid bodies.
    /// </summary>
    public const double DefaultMass = 1.0;

    /// <summary>
    /// Default bounciness (coefficient of restitution).
    /// </summary>
    /// <remarks>
    /// Realistic value for hard objects like plastic or wood.
    /// </remarks>
    public const double DefaultBounciness = 0.4;

    /// <summary>
    /// Default friction coefficient.
    /// </summary>
    /// <remarks>
    /// Typical value for dry surfaces.
    /// </remarks>
    public const double DefaultFriction = 0.5;

    /// <summary>
    /// Default drag coefficient.
    /// </summary>
    /// <remarks>
    /// Very low for solid objects in air.
    /// </remarks>
    public const double DefaultDrag = 0.001;

    /// <summary>
    /// Default bounding radius.
    /// </summary>
    public const double DefaultBoundingRadius = 0.5;

    /// <summary>
    /// Default object height.
    /// </summary>
    public const double DefaultHeight = 1.0;

    /// <summary>
    /// Minimum object height.
    /// </summary>
    public const double MinHeight = 0.1;

    #endregion

    #region Hashing Constants (for spatial grid)

    /// <summary>
    /// Prime number for X coordinate hashing.
    /// </summary>
    public const long HashPrimeX = 73856093L;

    /// <summary>
    /// Prime number for Y coordinate hashing.
    /// </summary>
    public const long HashPrimeY = 19349663L;

    /// <summary>
    /// Prime number for Z coordinate hashing.
    /// </summary>
    public const long HashPrimeZ = 83492791L;

    #endregion
}
