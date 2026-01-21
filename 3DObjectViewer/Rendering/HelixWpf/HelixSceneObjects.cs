using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Rendering.Abstractions;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.Rendering.HelixWpf;

/// <summary>
/// HelixToolkit.Wpf implementation of a box object.
/// </summary>
public sealed class HelixBoxObject : HelixSceneObject, IBoxObject
{
    private readonly BoxVisual3D _box;

    public HelixBoxObject(BoxVisual3D box) : base(box)
    {
        _box = box;
    }

    public double Width => _box.Width;
    double IBoxObject.Height => _box.Height;
    public double Length => _box.Length;

    public override Point3D Position
    {
        get => _box.Center;
        set => _box.Center = value;
    }

    public override double BoundingRadius => Math.Max(_box.Width, _box.Length) / 2;
    public override double Height => _box.Height;
}

/// <summary>
/// HelixToolkit.Wpf implementation of a sphere object.
/// </summary>
public sealed class HelixSphereObject : HelixSceneObject, ISphereObject
{
    private readonly SphereVisual3D _sphere;

    public HelixSphereObject(SphereVisual3D sphere) : base(sphere)
    {
        _sphere = sphere;
    }

    public double Radius => _sphere.Radius;

    public override Point3D Position
    {
        get => _sphere.Center;
        set => _sphere.Center = value;
    }

    public override double BoundingRadius => _sphere.Radius;
    public override double Height => _sphere.Radius * 2;
}

/// <summary>
/// HelixToolkit.Wpf implementation of a cylinder object.
/// </summary>
public sealed class HelixCylinderObject : HelixSceneObject, ICylinderObject
{
    private readonly PipeVisual3D _pipe;

    public HelixCylinderObject(PipeVisual3D pipe) : base(pipe)
    {
        _pipe = pipe;
    }

    public double Diameter => _pipe.Diameter;
    public double CylinderHeight => (_pipe.Point2 - _pipe.Point1).Length;

    public override Point3D Position
    {
        get => new(
            (_pipe.Point1.X + _pipe.Point2.X) / 2,
            (_pipe.Point1.Y + _pipe.Point2.Y) / 2,
            (_pipe.Point1.Z + _pipe.Point2.Z) / 2);
        set
        {
            var halfHeight = CylinderHeight / 2;
            _pipe.Point1 = new Point3D(value.X, value.Y, value.Z - halfHeight);
            _pipe.Point2 = new Point3D(value.X, value.Y, value.Z + halfHeight);
        }
    }

    public override double BoundingRadius => _pipe.Diameter / 2;
    public override double Height => CylinderHeight;
}

/// <summary>
/// HelixToolkit.Wpf implementation of a cone object.
/// </summary>
public sealed class HelixConeObject : HelixSceneObject, IConeObject
{
    private readonly TruncatedConeVisual3D _cone;

    public HelixConeObject(TruncatedConeVisual3D cone) : base(cone)
    {
        _cone = cone;
    }

    public double BaseRadius => _cone.BaseRadius;
    public double TopRadius => _cone.TopRadius;
    public double ConeHeight => _cone.Height;

    public override Point3D Position
    {
        get => new(_cone.Origin.X, _cone.Origin.Y, _cone.Origin.Z + _cone.Height / 2);
        set => _cone.Origin = new Point3D(value.X, value.Y, value.Z - _cone.Height / 2);
    }

    public override double BoundingRadius => Math.Max(_cone.BaseRadius, _cone.TopRadius);
    public override double Height => _cone.Height;
}

/// <summary>
/// HelixToolkit.Wpf implementation of a torus object.
/// </summary>
public sealed class HelixTorusObject : HelixSceneObject, ITorusObject
{
    private readonly TorusVisual3D _torus;

    public HelixTorusObject(TorusVisual3D torus) : base(torus)
    {
        _torus = torus;
    }

    public double TorusDiameter => _torus.TorusDiameter;
    public double TubeDiameter => _torus.TubeDiameter;

    public override Point3D Position
    {
        get
        {
            if (_torus.Transform is TranslateTransform3D translate)
                return new Point3D(translate.OffsetX, translate.OffsetY, translate.OffsetZ);
            return new Point3D(0, 0, 0);
        }
        set => _torus.Transform = new TranslateTransform3D(value.X, value.Y, value.Z);
    }

    public override double BoundingRadius => _torus.TorusDiameter / 2 + _torus.TubeDiameter / 2;
    public override double Height => _torus.TubeDiameter;
}
