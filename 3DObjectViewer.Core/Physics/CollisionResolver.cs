using System.Numerics;
using System.Runtime.CompilerServices;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Resolves collisions between physics bodies using impulse-based dynamics.
/// </summary>
/// <remarks>
/// <para>
/// This class handles the physics response to collisions, including:
/// <list type="bullet">
///   <item>Position correction to separate overlapping objects</item>
///   <item>Velocity impulse calculation using conservation of momentum</item>
///   <item>Friction impulse for tangential velocity</item>
/// </list>
/// </para>
/// <para>
/// <b>Physics model:</b> Uses impulse-based collision response with
/// coefficient of restitution (bounciness) and Coulomb friction model.
/// </para>
/// </remarks>
public static class CollisionResolver
{
    /// <summary>
    /// Resolves a collision between two bodies.
    /// </summary>
    /// <param name="posA">Position of body A (modified if collision occurs).</param>
    /// <param name="posB">Position of body B (modified if collision occurs).</param>
    /// <param name="velA">Velocity of body A (modified if collision occurs).</param>
    /// <param name="velB">Velocity of body B (modified if collision occurs).</param>
    /// <param name="radiusA">Bounding radius of body A.</param>
    /// <param name="radiusB">Bounding radius of body B.</param>
    /// <param name="massA">Mass of body A.</param>
    /// <param name="massB">Mass of body B.</param>
    /// <param name="bouncinessA">Bounciness of body A.</param>
    /// <param name="bouncinessB">Bounciness of body B.</param>
    /// <param name="frictionA">Friction of body A.</param>
    /// <param name="frictionB">Friction of body B.</param>
    /// <param name="isKinematicA">Whether body A is kinematic (immovable).</param>
    /// <param name="isKinematicB">Whether body B is kinematic (immovable).</param>
    /// <returns>True if a collision was detected and resolved.</returns>
    public static bool Resolve(
        ref Vector3 posA, ref Vector3 posB,
        ref Vector3 velA, ref Vector3 velB,
        float radiusA, float radiusB,
        float massA, float massB,
        float bouncinessA, float bouncinessB,
        float frictionA, float frictionB,
        bool isKinematicA, bool isKinematicB)
    {
        // Detect collision
        if (!CollisionDetector.GetCollisionInfo(posA, posB, radiusA, radiusB, out var normal, out float overlap))
        {
            return false;
        }

        // Separate the objects
        SeparateBodies(ref posA, ref posB, normal, overlap, massA, massB, isKinematicA, isKinematicB);

        // Calculate and apply velocity impulse
        ApplyImpulse(ref velA, ref velB, normal, massA, massB, bouncinessA, bouncinessB,
            frictionA, frictionB, isKinematicA, isKinematicB);

        return true;
    }

    /// <summary>
    /// Separates two overlapping bodies based on their masses.
    /// </summary>
    private static void SeparateBodies(
        ref Vector3 posA, ref Vector3 posB,
        Vector3 normal, float overlap,
        float massA, float massB,
        bool isKinematicA, bool isKinematicB)
    {
        float invMassA = isKinematicA ? 0 : 1.0f / massA;
        float invMassB = isKinematicB ? 0 : 1.0f / massB;
        float totalInvMass = invMassA + invMassB;

        if (totalInvMass < PhysicsConstants.MinDistance)
        {
            return; // Both kinematic
        }

        Vector3 separation = normal * (overlap * PhysicsConstants.SeparationFactor / totalInvMass);

        if (!isKinematicA)
        {
            posA -= separation * invMassA;
        }

        if (!isKinematicB)
        {
            posB += separation * invMassB;
        }
    }

    /// <summary>
    /// Applies velocity impulse based on collision physics.
    /// </summary>
    private static void ApplyImpulse(
        ref Vector3 velA, ref Vector3 velB,
        Vector3 normal,
        float massA, float massB,
        float bouncinessA, float bouncinessB,
        float frictionA, float frictionB,
        bool isKinematicA, bool isKinematicB)
    {
        // Calculate relative velocity
        Vector3 relativeVel = velA - velB;
        float velocityAlongNormal = Vector3.Dot(relativeVel, normal);

        // Objects moving apart - no impulse needed
        if (velocityAlongNormal > 0)
        {
            return;
        }

        // Calculate inverse masses
        float invMassA = isKinematicA ? 0 : 1.0f / massA;
        float invMassB = isKinematicB ? 0 : 1.0f / massB;
        float totalInvMass = invMassA + invMassB;

        if (totalInvMass < PhysicsConstants.MinDistance)
        {
            return; // Both kinematic
        }

        // Calculate restitution with damping
        float restitution = (bouncinessA + bouncinessB) * 0.5f * PhysicsConstants.CollisionDamping;

        // Calculate impulse magnitude using conservation of momentum
        float impulseMagnitude = -(1.0f + restitution) * velocityAlongNormal / totalInvMass;
        impulseMagnitude = Math.Clamp(impulseMagnitude, -PhysicsConstants.MaxImpulse, PhysicsConstants.MaxImpulse);

        // Apply impulse
        Vector3 impulse = normal * impulseMagnitude;

        if (!isKinematicA)
        {
            velA += impulse * invMassA;
        }

        if (!isKinematicB)
        {
            velB -= impulse * invMassB;
        }

        // Apply friction
        ApplyFriction(ref velA, ref velB, normal, relativeVel, velocityAlongNormal,
            MathF.Abs(impulseMagnitude), invMassA, invMassB, frictionA, frictionB,
            isKinematicA, isKinematicB);
    }

    /// <summary>
    /// Applies friction impulse during collision.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyFriction(
        ref Vector3 velA, ref Vector3 velB,
        Vector3 normal, Vector3 relativeVel, float velocityAlongNormal,
        float normalImpulseMagnitude,
        float invMassA, float invMassB,
        float frictionA, float frictionB,
        bool isKinematicA, bool isKinematicB)
    {
        // Calculate tangent vector (velocity perpendicular to normal)
        Vector3 tangent = relativeVel - normal * velocityAlongNormal;
        float tangentLengthSquared = Vector3.Dot(tangent, tangent);

        if (tangentLengthSquared < PhysicsConstants.MinDistance * PhysicsConstants.MinDistance)
        {
            return;
        }

        float tangentLength = MathF.Sqrt(tangentLengthSquared);
        tangent /= tangentLength;

        float totalInvMass = invMassA + invMassB;
        if (totalInvMass < PhysicsConstants.MinDistance)
        {
            return;
        }

        // Calculate friction impulse magnitude
        float frictionMagnitude = tangentLength / totalInvMass;

        // Use average friction coefficient with Coulomb model
        float friction = (frictionA + frictionB) * 0.5f;
        float maxFriction = normalImpulseMagnitude * friction;
        frictionMagnitude = MathF.Min(frictionMagnitude, maxFriction);

        Vector3 frictionImpulse = tangent * frictionMagnitude;

        if (!isKinematicA)
        {
            velA -= frictionImpulse * invMassA;
        }

        if (!isKinematicB)
        {
            velB += frictionImpulse * invMassB;
        }
    }
}
