using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using _3DObjectViewer.Core.Physics;

namespace _3DObjectViewer.Physics;

/// <summary>
/// Helper class for creating rigid bodies and syncing physics state to visuals.
/// </summary>
/// <remarks>
/// <para>
/// This class bridges the gap between the physics engine (which uses RigidBody)
/// and the rendering layer (which uses Visual3D). It handles:
/// </para>
/// <list type="bullet">
///   <item><description>Creating RigidBody instances with correct dimensions from Visual3D</description></item>
///   <item><description>Configuring physics properties based on object type</description></item>
///   <item><description>Syncing physics positions and rotations back to visual objects</description></item>
/// </list>
/// <para>
/// <b>Performance note:</b> Visual property changes (Center, Origin, Point1/Point2) trigger
/// mesh regeneration in HelixToolkit. This class uses Transform3D for position updates
/// to avoid this expensive operation.
/// </para>
/// <para>
/// <b>Memory optimization:</b> Transform3D objects are cached and reused per body to avoid
/// allocating new objects every physics frame.
/// </para>
/// </remarks>
public static class PhysicsHelper
{
    // Cache for mapping body Id to Visual3D for position sync
    private static readonly Dictionary<Guid, Visual3D> _bodyToVisual = [];
    
    // Track which visuals have been normalized (moved to origin for transform-based updates)
    private static readonly HashSet<Guid> _normalizedVisuals = [];
    
    // Cache for reusable TranslateTransform3D objects (for translation-only updates)
    private static readonly Dictionary<Guid, TranslateTransform3D> _translateTransformCache = [];
    
    // Cache for reusable MatrixTransform3D objects (for rotation + translation updates)
    private static readonly Dictionary<Guid, MatrixTransform3D> _matrixTransformCache = [];
    
    // Track last rotation state to avoid switching transform types unnecessarily
    private static readonly Dictionary<Guid, bool> _lastHadRotation = [];

    /// <summary>
    /// Creates a rigid body from a Visual3D object.
    /// </summary>
    /// <param name="visual">The Visual3D to create a physics body for.</param>
    /// <param name="position">The initial position.</param>
    /// <returns>A configured RigidBody.</returns>
    public static RigidBody CreateRigidBody(Visual3D visual, Point3D position)
    {
        var (radius, height) = GetObjectDimensions(visual);
        var bodyId = Guid.NewGuid();
        var body = new RigidBody(bodyId, position, radius, height);

        // Set physics properties based on object type
        ConfigurePhysicsProperties(body, visual);

        // Cache the visual for transform updates
        _bodyToVisual[bodyId] = visual;
        
        // Pre-create cached transforms for this body
        _translateTransformCache[bodyId] = new TranslateTransform3D(position.X, position.Y, position.Z);
        _matrixTransformCache[bodyId] = new MatrixTransform3D();
        _lastHadRotation[bodyId] = false;

        // Normalize the visual immediately (move to origin, use transform for position)
        NormalizeVisual(bodyId, visual, position, height);

        return body;
    }

    /// <summary>
    /// Removes the cached visual reference for a body and disposes the visual's geometry.
    /// </summary>
    public static void RemoveVisualCache(Guid bodyId)
    {
        if (_bodyToVisual.TryGetValue(bodyId, out var visual))
        {
            DisposeVisualGeometry(visual);
            _bodyToVisual.Remove(bodyId);
        }
        _normalizedVisuals.Remove(bodyId);
        _translateTransformCache.Remove(bodyId);
        _matrixTransformCache.Remove(bodyId);
        _lastHadRotation.Remove(bodyId);
    }

    /// <summary>
    /// Clears all cached visual references and disposes their geometry.
    /// </summary>
    public static void ClearVisualCache()
    {
        foreach (var visual in _bodyToVisual.Values)
        {
            DisposeVisualGeometry(visual);
        }
        _bodyToVisual.Clear();
        _normalizedVisuals.Clear();
        _translateTransformCache.Clear();
        _matrixTransformCache.Clear();
        _lastHadRotation.Clear();
    }

    /// <summary>
    /// Disposes the geometry data of a Visual3D to free memory.
    /// This clears the internal MeshGeometry3D collections (Positions, TriangleIndices, etc.)
    /// which can hold large arrays that are not automatically garbage collected.
    /// </summary>
    /// <param name="visual">The Visual3D to dispose.</param>
    public static void DisposeVisualGeometry(Visual3D visual)
    {
        if (visual is MeshElement3D meshElement)
        {
            // Get the underlying Model3D and clear its geometry
            var model = meshElement.Model;
            if (model is GeometryModel3D geometryModel)
            {
                ClearMeshGeometry(geometryModel.Geometry as MeshGeometry3D);
                
                // Clear material references
                geometryModel.Material = null;
                geometryModel.BackMaterial = null;
            }
        }
        else if (visual is ModelVisual3D modelVisual)
        {
            ClearModel3D(modelVisual.Content);
            modelVisual.Content = null;
        }
        
        // Clear the transform to release any references
        visual.Transform = null;
    }

    /// <summary>
    /// Recursively clears a Model3D and its children.
    /// </summary>
    private static void ClearModel3D(Model3D? model)
    {
        if (model is null) return;

        if (model is GeometryModel3D geometryModel)
        {
            ClearMeshGeometry(geometryModel.Geometry as MeshGeometry3D);
            geometryModel.Material = null;
            geometryModel.BackMaterial = null;
        }
        else if (model is Model3DGroup group)
        {
            foreach (var child in group.Children)
            {
                ClearModel3D(child);
            }
            group.Children.Clear();
        }
    }

    /// <summary>
    /// Clears the collections in a MeshGeometry3D to free memory.
    /// </summary>
    private static void ClearMeshGeometry(MeshGeometry3D? mesh)
    {
        if (mesh is null || mesh.IsFrozen) return;

        // Clear all the large collections that hold geometry data
        // These are the Point3D[], Int32[], and Point[] arrays identified by the profiler
        mesh.Positions?.Clear();
        mesh.TriangleIndices?.Clear();
        mesh.Normals?.Clear();
        mesh.TextureCoordinates?.Clear();
    }

    /// <summary>
    /// Gets the bounding dimensions of an object.
    /// </summary>
    private static (double radius, double height) GetObjectDimensions(Visual3D visual)
    {
        double radius = 0.5;
        double height = 1.0;

        switch (visual)
        {
            case BoxVisual3D box:
                height = box.Height;
                radius = Math.Max(box.Width, box.Length) / 2;
                break;

            case SphereVisual3D sphere:
                radius = sphere.Radius;
                height = sphere.Radius * 2;
                break;

            case PipeVisual3D pipe:
                radius = pipe.Diameter / 2;
                height = (pipe.Point2 - pipe.Point1).Length;
                break;

            case TruncatedConeVisual3D cone:
                radius = Math.Max(cone.BaseRadius, cone.TopRadius);
                height = cone.Height;
                break;

            case TorusVisual3D torus:
                radius = torus.TorusDiameter / 2 + torus.TubeDiameter / 2;
                height = torus.TubeDiameter;
                break;

            default:
                var bounds = Visual3DHelper.FindBounds(visual, Transform3D.Identity);
                if (!bounds.IsEmpty)
                {
                    height = bounds.SizeZ;
                    radius = Math.Max(bounds.SizeX, bounds.SizeY) / 2;
                }
                break;
        }

        return (radius, Math.Max(height, 0.1));
    }

    /// <summary>
    /// Configures physics properties based on object type.
    /// </summary>
    private static void ConfigurePhysicsProperties(RigidBody body, Visual3D visual)
    {
        (body.Bounciness, body.Friction, body.Mass) = visual switch
        {
            SphereVisual3D => (0.6, 0.3, 1.0),
            BoxVisual3D => (0.3, 0.6, 2.0),
            PipeVisual3D => (0.35, 0.5, 1.5),
            TruncatedConeVisual3D => (0.3, 0.55, 1.2),
            TorusVisual3D => (0.5, 0.4, 0.8),
            _ => (0.4, 0.5, 1.0)
        };
        
        body.Drag = 0.001;
    }

    /// <summary>
    /// Normalizes a visual by moving its geometry to the origin and applying a transform.
    /// This is done once per object to avoid expensive mesh regeneration on updates.
    /// </summary>
    private static void NormalizeVisual(Guid bodyId, Visual3D visual, Point3D position, double height)
    {
        switch (visual)
        {
            case BoxVisual3D box:
                box.Center = new Point3D(0, 0, 0);
                break;

            case SphereVisual3D sphere:
                sphere.Center = new Point3D(0, 0, 0);
                break;

            case PipeVisual3D pipe:
                double halfHeight = height / 2;
                pipe.Point1 = new Point3D(0, 0, -halfHeight);
                pipe.Point2 = new Point3D(0, 0, halfHeight);
                break;

            case TruncatedConeVisual3D cone:
                cone.Origin = new Point3D(0, 0, -height / 2);
                break;
        }

        // Apply initial transform using cached transform
        if (_translateTransformCache.TryGetValue(bodyId, out var translateTransform))
        {
            translateTransform.OffsetX = position.X;
            translateTransform.OffsetY = position.Y;
            translateTransform.OffsetZ = position.Z;
            visual.Transform = translateTransform;
        }
        else
        {
            visual.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);
        }
        _normalizedVisuals.Add(bodyId);
    }

    /// <summary>
    /// Updates the visual transform based on rigid body state.
    /// Uses cached transform objects to avoid allocations.
    /// </summary>
    /// <param name="body">The rigid body with updated position and orientation.</param>
    public static void ApplyTransformToVisual(RigidBody body)
    {
        if (!_bodyToVisual.TryGetValue(body.Id, out var visual))
            return;

        var position = body.Position;
        var orientation = body.Orientation;
        bool hasRotation = !IsIdentityQuaternion(orientation);
        
        // Check if we had rotation last frame to determine if we need to switch transform types
        bool lastHadRotation = _lastHadRotation.GetValueOrDefault(body.Id, false);

        if (!hasRotation)
        {
            // Use cached TranslateTransform3D - just update the offsets
            if (_translateTransformCache.TryGetValue(body.Id, out var translateTransform))
            {
                translateTransform.OffsetX = position.X;
                translateTransform.OffsetY = position.Y;
                translateTransform.OffsetZ = position.Z;
                
                // Only reassign if we switched from rotation to no-rotation
                if (lastHadRotation || visual.Transform != translateTransform)
                {
                    visual.Transform = translateTransform;
                }
            }
        }
        else
        {
            // Use cached MatrixTransform3D - update the matrix in place
            if (_matrixTransformCache.TryGetValue(body.Id, out var matrixTransform))
            {
                var matrix = Matrix3D.Identity;
                matrix.Rotate(orientation);
                matrix.Translate(new Vector3D(position.X, position.Y, position.Z));
                matrixTransform.Matrix = matrix;
                
                // Only reassign if we switched from no-rotation to rotation
                if (!lastHadRotation || visual.Transform != matrixTransform)
                {
                    visual.Transform = matrixTransform;
                }
            }
        }
        
        _lastHadRotation[body.Id] = hasRotation;
    }

    /// <summary>
    /// Checks if a quaternion is effectively the identity (no rotation).
    /// </summary>
    private static bool IsIdentityQuaternion(Quaternion q)
    {
        const double tolerance = 0.0001;
        return Math.Abs(q.X) < tolerance &&
               Math.Abs(q.Y) < tolerance &&
               Math.Abs(q.Z) < tolerance &&
               Math.Abs(q.W - 1.0) < tolerance;
    }
}
