using System.Numerics;
using System.Runtime.CompilerServices;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Handles force application and position integration for physics simulation.
/// </summary>
/// <remarks>
/// <para>
/// This class provides SIMD-optimized methods for:
/// <list type="bullet">
///   <item>Applying gravity (constant acceleration downward)</item>
///   <item>Applying drag (velocity-dependent resistance)</item>
///   <item>Integrating position from velocity (Euler integration)</item>
/// </list>
/// </para>
/// <para>
/// <b>Coordinate system:</b> Z is vertical (up), gravity pulls in -Z direction.
/// </para>
/// </remarks>
public static class ForceIntegrator
{
    /// <summary>
    /// Applies gravity and drag forces to a velocity.
    /// </summary>
    /// <param name="velocity">Current velocity (modified in place conceptually, returned as new value).</param>
    /// <param name="gravity">Gravity acceleration magnitude (applied in -Z direction).</param>
    /// <param name="drag">Drag coefficient (0 = no drag, higher = more resistance).</param>
    /// <param name="deltaTime">Time step in seconds.</param>
    /// <returns>Updated velocity after applying forces.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ApplyForces(Vector3 velocity, float gravity, float drag, float deltaTime)
    {
        // Apply drag as a velocity multiplier (exponential decay approximation)
        float dragFactor = 1.0f - drag * deltaTime;
        velocity *= dragFactor;

        // Apply gravity (negative Z direction)
        velocity.Z -= gravity * deltaTime;

        return velocity;
    }

    /// <summary>
    /// Updates position based on velocity (Euler integration).
    /// </summary>
    /// <param name="position">Current position.</param>
    /// <param name="velocity">Current velocity.</param>
    /// <param name="deltaTime">Time step in seconds.</param>
    /// <returns>New position after integration.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 IntegratePosition(Vector3 position, Vector3 velocity, float deltaTime)
    {
        return position + velocity * deltaTime;
    }

    /// <summary>
    /// Applies an impulse to a velocity.
    /// </summary>
    /// <param name="velocity">Current velocity.</param>
    /// <param name="impulse">Impulse to apply.</param>
    /// <param name="inverseMass">Inverse mass of the body (1/mass).</param>
    /// <returns>Updated velocity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ApplyImpulse(Vector3 velocity, Vector3 impulse, float inverseMass)
    {
        return velocity + impulse * inverseMass;
    }

    /// <summary>
    /// Clamps velocity to prevent unrealistic speeds.
    /// </summary>
    /// <param name="velocity">Velocity to clamp.</param>
    /// <param name="maxSpeed">Maximum allowed speed.</param>
    /// <returns>Clamped velocity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ClampVelocity(Vector3 velocity, float maxSpeed)
    {
        float speedSquared = Vector3.Dot(velocity, velocity);
        float maxSpeedSquared = maxSpeed * maxSpeed;

        if (speedSquared > maxSpeedSquared)
        {
            float scale = maxSpeed / MathF.Sqrt(speedSquared);
            return velocity * scale;
        }

        return velocity;
    }
}
