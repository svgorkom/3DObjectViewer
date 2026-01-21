using System.Numerics;
using System.Runtime.CompilerServices;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Handles collision detection between physics bodies.
/// </summary>
/// <remarks>
/// <para>
/// This class provides SIMD-optimized collision detection algorithms.
/// Currently implements sphere-sphere collision detection which is
/// efficient and provides good approximations for most objects.
/// </para>
/// <para>
/// <b>Collision detection is a two-phase process:</b>
/// <list type="number">
///   <item><b>Broad phase:</b> Uses <see cref="SpatialGrid"/> to quickly eliminate distant pairs</item>
///   <item><b>Narrow phase:</b> Uses this class for precise collision tests</item>
/// </list>
/// </para>
/// </remarks>
public static class CollisionDetector
{
    /// <summary>
    /// Checks if two spheres are colliding.
    /// </summary>
    /// <param name="posA">Center position of sphere A.</param>
    /// <param name="posB">Center position of sphere B.</param>
    /// <param name="radiusA">Radius of sphere A.</param>
    /// <param name="radiusB">Radius of sphere B.</param>
    /// <param name="delta">Output: Vector from A to B.</param>
    /// <param name="distanceSquared">Output: Squared distance between centers.</param>
    /// <returns>True if the spheres are overlapping.</returns>
    /// <remarks>
    /// Uses squared distance comparison to avoid expensive sqrt operation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckSphereCollision(
        Vector3 posA,
        Vector3 posB,
        float radiusA,
        float radiusB,
        out Vector3 delta,
        out float distanceSquared)
    {
        delta = posB - posA;
        distanceSquared = Vector3.Dot(delta, delta);
        float minDistance = radiusA + radiusB;
        return distanceSquared < minDistance * minDistance;
    }

    /// <summary>
    /// Checks if two spheres are colliding with minimal output.
    /// </summary>
    /// <param name="posA">Center position of sphere A.</param>
    /// <param name="posB">Center position of sphere B.</param>
    /// <param name="radiusA">Radius of sphere A.</param>
    /// <param name="radiusB">Radius of sphere B.</param>
    /// <returns>True if the spheres are overlapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CheckSphereCollision(
        Vector3 posA,
        Vector3 posB,
        float radiusA,
        float radiusB)
    {
        Vector3 delta = posB - posA;
        float distanceSquared = Vector3.Dot(delta, delta);
        float minDistance = radiusA + radiusB;
        return distanceSquared < minDistance * minDistance;
    }

    /// <summary>
    /// Calculates detailed collision information between two spheres.
    /// </summary>
    /// <param name="posA">Center position of sphere A.</param>
    /// <param name="posB">Center position of sphere B.</param>
    /// <param name="radiusA">Radius of sphere A.</param>
    /// <param name="radiusB">Radius of sphere B.</param>
    /// <param name="normal">Output: Collision normal (from A to B), normalized.</param>
    /// <param name="overlap">Output: Amount of overlap (penetration depth).</param>
    /// <returns>True if the spheres are colliding.</returns>
    public static bool GetCollisionInfo(
        Vector3 posA,
        Vector3 posB,
        float radiusA,
        float radiusB,
        out Vector3 normal,
        out float overlap)
    {
        Vector3 delta = posB - posA;
        float distanceSquared = Vector3.Dot(delta, delta);
        float minDistance = radiusA + radiusB;
        float minDistanceSquared = minDistance * minDistance;

        if (distanceSquared >= minDistanceSquared)
        {
            normal = Vector3.Zero;
            overlap = 0;
            return false;
        }

        float distance = MathF.Sqrt(distanceSquared);

        if (distance < PhysicsConstants.MinDistance)
        {
            // Objects are at the same position - use default direction
            normal = Vector3.UnitZ;
            distance = PhysicsConstants.MinDistance;
        }
        else
        {
            normal = delta / distance;
        }

        overlap = minDistance - distance;
        return true;
    }

    /// <summary>
    /// Checks if a velocity is below the rest threshold.
    /// </summary>
    /// <param name="velocity">The velocity to check.</param>
    /// <returns>True if the velocity magnitude is below the rest threshold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBelowRestThreshold(Vector3 velocity)
    {
        return Vector3.Dot(velocity, velocity) < PhysicsConstants.RestThresholdSquared;
    }

    /// <summary>
    /// Calculates the squared distance between two points.
    /// </summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <returns>The squared distance between the points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 diff = a - b;
        return Vector3.Dot(diff, diff);
    }
}
