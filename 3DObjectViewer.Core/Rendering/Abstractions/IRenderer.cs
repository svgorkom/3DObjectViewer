using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Models;

namespace _3DObjectViewer.Core.Rendering.Abstractions;

/// <summary>
/// Defines the contract for a 3D rendering engine.
/// Implementations can use different underlying libraries (HelixToolkit.Wpf, SharpDX, etc.).
/// </summary>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Gets the name of the rendering engine.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the WPF control that displays the 3D scene.
    /// </summary>
    FrameworkElement ViewportControl { get; }

    /// <summary>
    /// Gets or sets the camera position.
    /// </summary>
    Point3D CameraPosition { get; set; }

    /// <summary>
    /// Gets or sets the camera look direction.
    /// </summary>
    Vector3D CameraLookDirection { get; set; }

    /// <summary>
    /// Gets or sets the camera up direction.
    /// </summary>
    Vector3D CameraUpDirection { get; set; }

    /// <summary>
    /// Initializes the renderer and prepares it for use.
    /// </summary>
    void Initialize();

    #region Object Management

    /// <summary>
    /// Creates a box/cube in the scene.
    /// </summary>
    ISceneObject CreateBox(double width, double height, double length, Point3D center, Color color);

    /// <summary>
    /// Creates a sphere in the scene.
    /// </summary>
    ISceneObject CreateSphere(double radius, Point3D center, Color color);

    /// <summary>
    /// Creates a cylinder in the scene.
    /// </summary>
    ISceneObject CreateCylinder(double diameter, Point3D point1, Point3D point2, Color color);

    /// <summary>
    /// Creates a cone in the scene.
    /// </summary>
    ISceneObject CreateCone(double baseRadius, double topRadius, double height, Point3D origin, Color color);

    /// <summary>
    /// Creates a torus in the scene.
    /// </summary>
    ISceneObject CreateTorus(double torusDiameter, double tubeDiameter, Point3D center, Color color);

    /// <summary>
    /// Adds an object to the scene.
    /// </summary>
    void AddObject(ISceneObject obj);

    /// <summary>
    /// Removes an object from the scene.
    /// </summary>
    void RemoveObject(ISceneObject obj);

    /// <summary>
    /// Clears all objects from the scene.
    /// </summary>
    void ClearObjects();

    /// <summary>
    /// Updates the position of an object.
    /// </summary>
    void UpdateObjectPosition(ISceneObject obj, Point3D newPosition);

    #endregion

    #region Selection

    /// <summary>
    /// Sets the selection highlight for an object.
    /// </summary>
    void SetSelection(ISceneObject? obj);

    /// <summary>
    /// Updates the selection box bounds.
    /// </summary>
    void UpdateSelectionBounds(Rect3D bounds);

    #endregion

    #region Lighting

    /// <summary>
    /// Updates all lights in the scene.
    /// </summary>
    void UpdateLights(IEnumerable<LightSource> lightSources);

    #endregion

    #region Hit Testing

    /// <summary>
    /// Performs hit testing at the specified screen position.
    /// </summary>
    /// <param name="position">The screen position to test.</param>
    /// <returns>The hit object and 3D position, or null if nothing was hit.</returns>
    (ISceneObject? Object, Point3D Position)? HitTest(Point position);

    /// <summary>
    /// Gets a ray from the camera through the specified screen position.
    /// </summary>
    (Point3D Origin, Vector3D Direction)? GetRay(Point position);

    #endregion

    #region Scene Management

    /// <summary>
    /// Gets the bounds of an object.
    /// </summary>
    Rect3D GetObjectBounds(ISceneObject obj);

    /// <summary>
    /// Captures mouse input on the viewport.
    /// </summary>
    void CaptureMouse();

    /// <summary>
    /// Releases mouse capture on the viewport.
    /// </summary>
    void ReleaseMouseCapture();

    #endregion
}
