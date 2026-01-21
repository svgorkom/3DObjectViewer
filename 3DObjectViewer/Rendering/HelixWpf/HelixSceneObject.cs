using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Rendering.Abstractions;

namespace _3DObjectViewer.Rendering.HelixWpf;

/// <summary>
/// Base class for HelixToolkit.Wpf scene objects.
/// </summary>
public abstract class HelixSceneObject : ISceneObject
{
    protected HelixSceneObject(Visual3D visual)
    {
        Id = Guid.NewGuid();
        Visual = visual;
        visual.Transform = Transform3D.Identity;
    }

    /// <inheritdoc/>
    public Guid Id { get; }

    /// <summary>
    /// Gets the underlying Visual3D.
    /// </summary>
    public Visual3D Visual { get; }

    /// <inheritdoc/>
    public object NativeObject => Visual;

    /// <inheritdoc/>
    public abstract Point3D Position { get; set; }

    /// <inheritdoc/>
    public Transform3D Transform
    {
        get => Visual.Transform ?? Transform3D.Identity;
        set => Visual.Transform = value ?? Transform3D.Identity;
    }

    /// <inheritdoc/>
    public abstract double BoundingRadius { get; }

    /// <inheritdoc/>
    public abstract double Height { get; }
}
