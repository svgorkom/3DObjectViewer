using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuUtilities;

namespace _3DObjectViewer.Core.Physics.Bepu;

/// <summary>
/// Pose integrator callbacks for BEPU physics simulation.
/// </summary>
/// <remarks>
/// Handles gravity application and velocity damping during simulation steps.
/// Damping values are kept minimal for realistic hard-surface physics.
/// </remarks>
internal struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    /// <summary>
    /// Shared gravity value that can be updated at runtime.
    /// Using a class wrapper to allow the struct to reference mutable state.
    /// </summary>
    internal sealed class GravityReference
    {
        public float GravityZ;
        
        public GravityReference(float gravityZ) => GravityZ = gravityZ;
    }
    
    private readonly GravityReference _gravityRef;
    private Vector<float> _gravityWideDt;
    private Vector<float> _linearDampingDt;
    private Vector<float> _angularDampingDt;

    /// <summary>
    /// Linear velocity damping per second (1.0 = no damping).
    /// </summary>
    public float LinearDampingPerSecond { get; init; }

    /// <summary>
    /// Angular velocity damping per second (1.0 = no damping).
    /// </summary>
    public float AngularDampingPerSecond { get; init; }

    public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public readonly bool AllowSubstepsForUnconstrainedBodies => false;
    public readonly bool IntegrateVelocityForKinematics => false;

    /// <summary>
    /// Creates a pose integrator with the specified gravity reference.
    /// </summary>
    /// <param name="gravityRef">Reference to the gravity value (allows runtime updates).</param>
    public PoseIntegratorCallbacks(GravityReference gravityRef) : this()
    {
        _gravityRef = gravityRef;
        LinearDampingPerSecond = 0.995f;  // Very slight air resistance
        AngularDampingPerSecond = 0.99f;  // Slightly more to prevent infinite spinning
    }

    /// <summary>
    /// Creates callbacks with default realistic damping.
    /// </summary>
    /// <param name="gravityMagnitude">Gravity magnitude (positive, will be applied as -Z).</param>
    public static PoseIntegratorCallbacks CreateDefault(float gravityMagnitude) => 
        new(new GravityReference(-gravityMagnitude))
        {
            LinearDampingPerSecond = 0.995f,
            AngularDampingPerSecond = 0.99f
        };
    
    /// <summary>
    /// Creates callbacks with a shared gravity reference for runtime updates.
    /// </summary>
    /// <param name="gravityRef">Shared gravity reference.</param>
    public static PoseIntegratorCallbacks CreateWithReference(GravityReference gravityRef) => 
        new(gravityRef)
        {
            LinearDampingPerSecond = 0.995f,
            AngularDampingPerSecond = 0.99f
        };

    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
        // Read current gravity value each frame (allows dynamic updates)
        _gravityWideDt = Vector.Create(_gravityRef.GravityZ * dt);
        
        // Apply damping as power of dt to be frame-rate independent
        float linearDamping = MathF.Pow(LinearDampingPerSecond, dt);
        float angularDamping = MathF.Pow(AngularDampingPerSecond, dt);
        
        _linearDampingDt = Vector.Create(linearDamping);
        _angularDampingDt = Vector.Create(angularDamping);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IntegrateVelocity(
        Vector<int> bodyIndices, 
        Vector3Wide position, 
        QuaternionWide orientation,
        BodyInertiaWide localInertia, 
        Vector<int> integrationMask, 
        int workerIndex,
        Vector<float> dt, 
        ref BodyVelocityWide velocity)
    {
        // Apply gravity (in Z direction)
        velocity.Linear.Z += _gravityWideDt;

        // Apply minimal damping
        velocity.Linear.X *= _linearDampingDt;
        velocity.Linear.Y *= _linearDampingDt;
        velocity.Linear.Z *= _linearDampingDt;
        velocity.Angular.X *= _angularDampingDt;
        velocity.Angular.Y *= _angularDampingDt;
        velocity.Angular.Z *= _angularDampingDt;
    }
}
