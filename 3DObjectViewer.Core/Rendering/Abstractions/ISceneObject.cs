using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace _3DObjectViewer.Core.Rendering.Abstractions;

/*
================================================================================
SCENE OBJECT ABSTRACTION LAYER
================================================================================

This file defines the abstraction for 3D objects in the scene, decoupling the
application logic from the specific 3D rendering library being used.

DESIGN PATTERN: Strategy Pattern + Abstract Factory
- ISceneObject is the abstract product
- Concrete implementations (HelixBoxObject, etc.) are created by IRenderer
- Allows runtime switching between rendering backends

OBJECT LIFECYCLE:
1. Created via IRenderer.CreateXxx() methods
2. Added to scene via IRenderer.AddObject()
3. Position updated via ISceneObject.Position property
4. Removed via IRenderer.RemoveObject()

COORDINATE SYSTEM:
- Position represents the center point of the object
- BoundingRadius is used for sphere-based collision detection
- Height is used for ground plane collision (Z-axis)

IMPLEMENTATION NOTES:
- NativeObject provides access to underlying library-specific object
- Transform property handles rotation/scaling beyond simple position
- Guid Id enables physics engine integration without library coupling
================================================================================
*/

/// <summary>
/// Represents a 3D object in the scene, abstracting away the underlying library implementation.
/// </summary>
/// <remarks>
/// <para>
/// This interface serves as the common contract for all 3D objects regardless of the
/// rendering backend (HelixToolkit.Wpf, SharpDX, native WPF, etc.).
/// </para>
/// <para>
/// <b>For AI/LLM Context:</b> When working with scene objects:
/// <list type="bullet">
///   <item>Use <see cref="Position"/> to get/set the object's center in world coordinates</item>
///   <item>Use <see cref="Id"/> to correlate with physics bodies in <see cref="Physics.PhysicsEngine"/></item>
///   <item>Use <see cref="NativeObject"/> only when you need library-specific functionality</item>
/// </list>
/// </para>
/// </remarks>
public interface ISceneObject
{
    /// <summary>
    /// Gets the unique identifier for this object.
    /// </summary>
    /// <remarks>
    /// This ID is used to:
    /// <list type="bullet">
    ///   <item>Link to physics rigid bodies in <see cref="Physics.RigidBody"/></item>
    ///   <item>Track objects across renderer switches</item>
    ///   <item>Manage shadows and selection state</item>
    /// </list>
    /// The ID is generated when the object is created and remains constant for its lifetime.
    /// </remarks>
    Guid Id { get; }

    /// <summary>
    /// Gets or sets the position of the object center in world coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The position represents the geometric center of the object, not its base or corner.
    /// </para>
    /// <para>
    /// <b>Coordinate System:</b>
    /// <list type="bullet">
    ///   <item>X: Horizontal axis (positive = right when viewing from default camera)</item>
    ///   <item>Y: Horizontal axis (positive = forward/away from default camera)</item>
    ///   <item>Z: Vertical axis (positive = up, Z=0 is ground level)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Setting this property updates the visual representation immediately.
    /// For physics-controlled objects, also update the corresponding <see cref="Physics.RigidBody.Position"/>.
    /// </para>
    /// </remarks>
    Point3D Position { get; set; }

    /// <summary>
    /// Gets or sets the transform applied to this object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="Transform3D.Identity"/> for objects positioned only via <see cref="Position"/>.
    /// </para>
    /// <para>
    /// <b>Warning:</b> Setting Transform to null may cause hit-testing failures in some
    /// rendering backends. Always use <see cref="Transform3D.Identity"/> instead of null.
    /// </para>
    /// </remarks>
    Transform3D Transform { get; set; }

    /// <summary>
    /// Gets the bounding radius for collision detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the radius of a sphere that fully contains the object, centered at <see cref="Position"/>.
    /// Used by <see cref="Physics.PhysicsEngine"/> for sphere-based collision detection.
    /// </para>
    /// <para>
    /// <b>Calculation by object type:</b>
    /// <list type="bullet">
    ///   <item>Sphere: Equal to the sphere's radius</item>
    ///   <item>Box: Half of the larger of width or length</item>
    ///   <item>Cylinder: Half of the diameter</item>
    ///   <item>Torus: Outer radius (TorusDiameter/2 + TubeDiameter/2)</item>
    /// </list>
    /// </para>
    /// </remarks>
    double BoundingRadius { get; }

    /// <summary>
    /// Gets the height of the object along the Z-axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="Physics.PhysicsEngine"/> for ground collision detection.
    /// The object's bottom is at <c>Position.Z - Height/2</c>.
    /// </para>
    /// <para>
    /// <b>Examples:</b>
    /// <list type="bullet">
    ///   <item>Sphere with radius 1.0: Height = 2.0</item>
    ///   <item>Box with height 1.5: Height = 1.5</item>
    ///   <item>Torus: Height = TubeDiameter (the vertical extent)</item>
    /// </list>
    /// </para>
    /// </remarks>
    double Height { get; }

    /// <summary>
    /// Gets the underlying visual object (library-specific).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Type varies by renderer:</b>
    /// <list type="bullet">
    ///   <item>HelixToolkit.Wpf: <c>Visual3D</c> (BoxVisual3D, SphereVisual3D, etc.)</item>
    ///   <item>HelixToolkit.SharpDX: <c>Element3D</c></item>
    ///   <item>Native WPF: <c>ModelVisual3D</c></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Warning:</b> Casting this to a specific type couples your code to that renderer.
    /// Prefer using <see cref="ISceneObject"/> methods when possible.
    /// </para>
    /// </remarks>
    object NativeObject { get; }
}

/// <summary>
/// Represents a box/cube 3D object with width, height, and length dimensions.
/// </summary>
public interface IBoxObject : ISceneObject
{
    /// <summary>Gets the width of the box along the X-axis.</summary>
    double Width { get; }
    
    /// <summary>Gets the height of the box along the Z-axis (vertical dimension).</summary>
    new double Height { get; }
    
    /// <summary>Gets the length of the box along the Y-axis.</summary>
    double Length { get; }
}

/// <summary>
/// Represents a sphere 3D object defined by its radius.
/// </summary>
public interface ISphereObject : ISceneObject
{
    /// <summary>
    /// Gets the radius of the sphere.
    /// </summary>
    double Radius { get; }
}

/// <summary>
/// Represents a cylinder 3D object defined by its diameter and height.
/// </summary>
public interface ICylinderObject : ISceneObject
{
    /// <summary>Gets the diameter of the cylinder's circular cross-section.</summary>
    double Diameter { get; }
    
    /// <summary>Gets the height of the cylinder along the Z-axis.</summary>
    double CylinderHeight { get; }
}

/// <summary>
/// Represents a cone or truncated cone (frustum) 3D object.
/// </summary>
public interface IConeObject : ISceneObject
{
    /// <summary>Gets the radius at the base (bottom) of the cone.</summary>
    double BaseRadius { get; }
    
    /// <summary>Gets the radius at the top of the cone. Zero for a pointed cone.</summary>
    double TopRadius { get; }
    
    /// <summary>Gets the height of the cone along the Z-axis.</summary>
    double ConeHeight { get; }
}

/// <summary>
/// Represents a torus (donut/ring shape) 3D object.
/// </summary>
public interface ITorusObject : ISceneObject
{
    /// <summary>Gets the major diameter (overall size) of the torus.</summary>
    double TorusDiameter { get; }
    
    /// <summary>Gets the minor diameter (tube thickness) of the torus.</summary>
    double TubeDiameter { get; }
}
