using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace _3DObjectViewer.Core.Physics.Bepu;

/// <summary>
/// Narrow phase collision callbacks for BEPU physics simulation.
/// </summary>
/// <remarks>
/// Configures material properties (friction, bounciness) for collisions
/// and filters which collisions should be processed.
/// </remarks>
internal struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    /// <summary>
    /// Friction coefficient for surface contacts.
    /// </summary>
    public float FrictionCoefficient { get; init; }

    /// <summary>
    /// Maximum velocity for collision recovery.
    /// </summary>
    public float MaximumRecoveryVelocity { get; init; }

    /// <summary>
    /// Spring frequency for contact constraint.
    /// </summary>
    public float SpringFrequency { get; init; }

    /// <summary>
    /// Spring damping ratio for contact constraint.
    /// </summary>
    public float SpringDampingRatio { get; init; }

    /// <summary>
    /// Creates callbacks with default hard surface properties.
    /// </summary>
    public static NarrowPhaseCallbacks CreateDefault() => new()
    {
        FrictionCoefficient = 0.6f,
        MaximumRecoveryVelocity = 2f,
        SpringFrequency = 30f,
        SpringDampingRatio = 1f
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        // Allow contacts if at least one body is dynamic
        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold<TManifold>(
        int workerIndex, 
        CollidablePair pair, 
        ref TManifold manifold, 
        out PairMaterialProperties pairMaterial)
        where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = FrictionCoefficient;
        pairMaterial.MaximumRecoveryVelocity = MaximumRecoveryVelocity;
        pairMaterial.SpringSettings = new SpringSettings(SpringFrequency, SpringDampingRatio);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConfigureContactManifold(
        int workerIndex, 
        CollidablePair pair, 
        int childIndexA, 
        int childIndexB, 
        ref ConvexContactManifold manifold)
    {
        return true;
    }

    public void Initialize(Simulation simulation)
    {
    }

    public void Dispose()
    {
    }
}
