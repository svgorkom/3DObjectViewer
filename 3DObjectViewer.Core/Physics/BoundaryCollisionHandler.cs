using System.Numerics;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Represents a boundary plane for collision detection.
/// </summary>
/// <param name="Normal">The outward-facing normal of the boundary (unit vector).</param>
/// <param name="Distance">The distance from origin along the normal (defines plane position).</param>
public readonly record struct BoundaryPlane(Vector3 Normal, float Distance)
{
    /// <summary>
    /// Calculates the signed distance from a point to this plane.
    /// Positive = point is in front of plane (outside boundary).
    /// Negative = point is behind plane (inside boundary).
    /// </summary>
    public float SignedDistance(Vector3 point) => Vector3.Dot(point, Normal) - Distance;
}

/// <summary>
/// Handles collisions with world boundaries using a unified plane-based approach.
/// </summary>
/// <remarks>
/// <para>
/// All boundaries (ground, walls, ceiling) are represented as planes with normals.
/// This provides consistent collision response regardless of boundary orientation.
/// </para>
/// <para>
/// <b>Coordinate system:</b> Z is vertical (up), ground is at Z = GroundLevel.
/// </para>
/// </remarks>
public sealed class BoundaryCollisionHandler
{
    private readonly List<BoundaryPlane> _boundaries = [];
    private float _groundLevel;
    private bool _bucketEnabled;

    /// <summary>
    /// Gets or sets the ground level (Z coordinate of the ground plane).
    /// </summary>
    public float GroundLevel
    {
        get => _groundLevel;
        set
        {
            _groundLevel = value;
            RebuildBoundaries();
        }
    }

    /// <summary>
    /// Gets whether bucket boundaries are enabled.
    /// </summary>
    public bool BucketEnabled => _bucketEnabled;

    // Bucket configuration (stored for rebuilding)
    private float _bucketMinX;
    private float _bucketMaxX;
    private float _bucketMinY;
    private float _bucketMaxY;
    private float _bucketHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundaryCollisionHandler"/> class.
    /// </summary>
    public BoundaryCollisionHandler()
    {
        RebuildBoundaries();
    }

    /// <summary>
    /// Configures rectangular bucket boundaries.
    /// </summary>
    public void SetBucketBounds(double minX, double maxX, double minY, double maxY, double height)
    {
        _bucketMinX = (float)Math.Min(minX, maxX);
        _bucketMaxX = (float)Math.Max(minX, maxX);
        _bucketMinY = (float)Math.Min(minY, maxY);
        _bucketMaxY = (float)Math.Max(minY, maxY);
        _bucketHeight = (float)height;

        // Only enable bucket if it has meaningful size
        const float minSize = 1.0f;
        _bucketEnabled = (_bucketMaxX - _bucketMinX) >= minSize &&
                         (_bucketMaxY - _bucketMinY) >= minSize;

        RebuildBoundaries();
    }

    /// <summary>
    /// Disables bucket wall collisions.
    /// </summary>
    public void DisableBucket()
    {
        _bucketEnabled = false;
        RebuildBoundaries();
    }

    /// <summary>
    /// Rebuilds the boundary plane list based on current configuration.
    /// </summary>
    private void RebuildBoundaries()
    {
        _boundaries.Clear();

        // Ground plane: normal pointing up (+Z), at ground level
        _boundaries.Add(new BoundaryPlane(Vector3.UnitZ, _groundLevel));

        if (_bucketEnabled)
        {
            // Wall planes: normals point inward (toward center of bucket)
            // Left wall (-X): normal points +X
            _boundaries.Add(new BoundaryPlane(Vector3.UnitX, _bucketMinX));
            // Right wall (+X): normal points -X
            _boundaries.Add(new BoundaryPlane(-Vector3.UnitX, -_bucketMaxX));
            // Front wall (-Y): normal points +Y
            _boundaries.Add(new BoundaryPlane(Vector3.UnitY, _bucketMinY));
            // Back wall (+Y): normal points -Y
            _boundaries.Add(new BoundaryPlane(-Vector3.UnitY, -_bucketMaxY));
        }
    }

    /// <summary>
    /// Handles all boundary collisions for a body.
    /// </summary>
    /// <param name="position">Body position (modified if collision occurs).</param>
    /// <param name="velocity">Body velocity (modified if collision occurs).</param>
    /// <param name="boundingRadius">Body's bounding radius.</param>
    /// <param name="height">Body's height.</param>
    /// <param name="bounciness">Body's bounciness coefficient.</param>
    /// <param name="friction">Body's friction coefficient.</param>
    /// <returns>True if a ground collision occurred.</returns>
    public bool HandleBoundaryCollisions(
        ref Vector3 position,
        ref Vector3 velocity,
        float boundingRadius,
        float height,
        float bounciness,
        float friction)
    {
        bool groundCollision = false;
        float halfHeight = height * 0.5f;

        foreach (var boundary in _boundaries)
        {
            // Calculate effective radius along boundary normal
            // For ground (Z normal), use half height; for walls (X/Y normal), use bounding radius
            float effectiveRadius = IsVerticalBoundary(boundary.Normal) ? halfHeight : boundingRadius;

            // Skip wall collisions if object is above bucket height
            if (!IsVerticalBoundary(boundary.Normal))
            {
                float bottomZ = position.Z - halfHeight;
                if (bottomZ >= _bucketHeight)
                {
                    continue;
                }
            }

            bool collided = HandlePlaneBoundaryCollision(
                ref position,
                ref velocity,
                boundary,
                effectiveRadius,
                bounciness,
                friction);

            // Track ground collision (ground is the first boundary, normal is +Z)
            if (collided && boundary.Normal.Z > 0.5f)
            {
                groundCollision = true;
            }
        }

        return groundCollision;
    }

    /// <summary>
    /// Checks if a boundary normal indicates a vertical surface (ground/ceiling).
    /// </summary>
    private static bool IsVerticalBoundary(Vector3 normal) => MathF.Abs(normal.Z) > 0.5f;

    /// <summary>
    /// Handles collision with a single boundary plane.
    /// </summary>
    private static bool HandlePlaneBoundaryCollision(
        ref Vector3 position,
        ref Vector3 velocity,
        BoundaryPlane boundary,
        float effectiveRadius,
        float bounciness,
        float friction)
    {
        // Calculate penetration depth
        float signedDistance = boundary.SignedDistance(position);
        float penetration = effectiveRadius - signedDistance;

        if (penetration <= 0)
        {
            return false; // No collision
        }

        // Correct position: push out along normal
        position += boundary.Normal * penetration;

        // Calculate velocity component along normal
        float velocityAlongNormal = Vector3.Dot(velocity, boundary.Normal);

        // Only respond if moving into the boundary
        if (velocityAlongNormal >= -PhysicsConstants.RestThreshold)
        {
            // Not moving into boundary fast enough, just zero out normal component if negative
            if (velocityAlongNormal < 0)
            {
                velocity -= boundary.Normal * velocityAlongNormal;
            }
            return false;
        }

        // Calculate bounce response
        float bounceSpeed = MathF.Abs(velocityAlongNormal) * bounciness * PhysicsConstants.WallDamping;

        // Decompose velocity into normal and tangential components
        Vector3 normalComponent = boundary.Normal * velocityAlongNormal;
        Vector3 tangentialComponent = velocity - normalComponent;

        // Apply friction to tangential velocity
        float frictionFactor = 1.0f - friction * PhysicsConstants.GroundFrictionMultiplier;

        if (bounceSpeed > PhysicsConstants.RestThreshold * 2)
        {
            // Bounce
            velocity = tangentialComponent * frictionFactor + boundary.Normal * bounceSpeed;
        }
        else
        {
            // Come to rest against this boundary (stronger friction)
            float strongFriction = 1.0f - friction * PhysicsConstants.StrongGroundFrictionMultiplier;
            velocity = tangentialComponent * strongFriction;
        }

        return true;
    }

    /// <summary>
    /// Checks if a body is grounded (on or near the ground).
    /// </summary>
    /// <param name="bottomZ">The Z coordinate of the body's bottom.</param>
    /// <returns>True if the body is grounded.</returns>
    public bool IsGrounded(float bottomZ)
    {
        return bottomZ <= _groundLevel + PhysicsConstants.GroundedThreshold;
    }
}
