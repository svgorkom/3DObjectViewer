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
    private readonly float _gravityZ;
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
    /// Creates a pose integrator with the specified gravity.
    /// </summary>
    /// <param name="gravity">Gravity vector (typically negative Z for downward).</param>
    public PoseIntegratorCallbacks(Vector3 gravity) : this()
    {
        _gravityZ = gravity.Z;
        LinearDampingPerSecond = 0.995f;  // Very slight air resistance
        AngularDampingPerSecond = 0.99f;  // Slightly more to prevent infinite spinning
    }

    /// <summary>
    /// Creates callbacks with default realistic damping.
    /// </summary>
    /// <param name="gravityMagnitude">Gravity magnitude (positive, will be applied as -Z).</param>
    public static PoseIntegratorCallbacks CreateDefault(float gravityMagnitude) => new(new Vector3(0, 0, -gravityMagnitude))
    {
        LinearDampingPerSecond = 0.995f,
        AngularDampingPerSecond = 0.99f
    };

    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
        _gravityWideDt = Vector.Create(_gravityZ * dt);
        
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
