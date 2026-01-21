using System.Windows.Media.Media3D;

namespace _3DObjectViewer.Core.Helpers;

/// <summary>
/// Represents a plane in 3D space for drag calculations.
/// </summary>
/// <remarks>
/// <para>
/// This class is used for calculating intersection points during object dragging.
/// The plane is typically oriented perpendicular to the camera's look direction,
/// passing through the object's initial position.
/// </para>
/// <para>
/// <b>Note:</b> Named DragPlane3D to avoid conflict with HelixToolkit.Wpf.Plane3D.
/// </para>
/// </remarks>
public class DragPlane3D
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DragPlane3D"/> class.
    /// </summary>
    /// <param name="position">A point on the plane.</param>
    /// <param name="normal">The normal vector of the plane.</param>
    public DragPlane3D(Point3D position, Vector3D normal)
    {
        Position = position;
        Normal = normal;
        Normal.Normalize();
    }

    /// <summary>
    /// Gets a point on the plane.
    /// </summary>
    public Point3D Position { get; }

    /// <summary>
    /// Gets the normal vector of the plane.
    /// </summary>
    public Vector3D Normal { get; }
}
