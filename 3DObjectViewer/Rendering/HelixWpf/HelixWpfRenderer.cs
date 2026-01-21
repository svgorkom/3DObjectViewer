using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Models;
using _3DObjectViewer.Core.Rendering.Abstractions;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.Rendering.HelixWpf;

/// <summary>
/// HelixToolkit.Wpf implementation of the renderer interface.
/// </summary>
public sealed class HelixWpfRenderer : IRenderer
{
    private readonly HelixViewport3D _viewport;
    private readonly Dictionary<Guid, ISceneObject> _objects = new();
    private readonly Dictionary<LightSource, ModelVisual3D> _lightVisuals = new();
    private readonly Dictionary<LightSource, ModelVisual3D> _lampFixtures = new();
    private readonly Dictionary<Guid, (double radiusX, double radiusY)> _boundsCache = new();
    
    private readonly BoundingBoxVisual3D _selectionBox;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="HelixWpfRenderer"/> class.
    /// </summary>
    public HelixWpfRenderer()
    {
        _viewport = new HelixViewport3D
        {
            ShowCoordinateSystem = true,
            ZoomExtentsWhenLoaded = true
        };
        
        _selectionBox = new BoundingBoxVisual3D
        {
            Diameter = 0.05,
            Transform = Transform3D.Identity
        };
    }

    /// <inheritdoc/>
    public string Name => "HelixToolkit.Wpf";

    /// <inheritdoc/>
    public FrameworkElement ViewportControl => _viewport;

    /// <inheritdoc/>
    public Point3D CameraPosition
    {
        get => _viewport.Camera.Position;
        set => _viewport.Camera.Position = value;
    }

    /// <inheritdoc/>
    public Vector3D CameraLookDirection
    {
        get => _viewport.Camera.LookDirection;
        set => _viewport.Camera.LookDirection = value;
    }

    /// <inheritdoc/>
    public Vector3D CameraUpDirection
    {
        get => _viewport.Camera.UpDirection;
        set => _viewport.Camera.UpDirection = value;
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        _viewport.Children.Add(_selectionBox);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        ClearObjects();
        _lightVisuals.Clear();
        _lampFixtures.Clear();
    }

    #region Object Management

    /// <inheritdoc/>
    public ISceneObject CreateBox(double width, double height, double length, Point3D center, Color color)
    {
        var material = CreateShinyFrozenMaterial(color);
        var box = new BoxVisual3D
        {
            Width = width,
            Height = height,
            Length = length,
            Center = center,
            Material = material,
            Transform = Transform3D.Identity
        };
        return new HelixBoxObject(box);
    }

    /// <inheritdoc/>
    public ISceneObject CreateSphere(double radius, Point3D center, Color color)
    {
        var material = CreateShinyFrozenMaterial(color);
        var sphere = new SphereVisual3D
        {
            Radius = radius,
            Center = center,
            Material = material,
            Transform = Transform3D.Identity
        };
        return new HelixSphereObject(sphere);
    }

    /// <inheritdoc/>
    public ISceneObject CreateCylinder(double diameter, Point3D point1, Point3D point2, Color color)
    {
        var material = CreateShinyFrozenMaterial(color);
        var pipe = new PipeVisual3D
        {
            Diameter = diameter,
            InnerDiameter = 0,
            Point1 = point1,
            Point2 = point2,
            Material = material,
            Transform = Transform3D.Identity
        };
        return new HelixCylinderObject(pipe);
    }

    /// <inheritdoc/>
    public ISceneObject CreateCone(double baseRadius, double topRadius, double height, Point3D origin, Color color)
    {
        var material = CreateShinyFrozenMaterial(color);
        var cone = new TruncatedConeVisual3D
        {
            BaseRadius = baseRadius,
            TopRadius = topRadius,
            Height = height,
            Origin = origin,
            Material = material,
            Transform = Transform3D.Identity
        };
        return new HelixConeObject(cone);
    }

    /// <inheritdoc/>
    public ISceneObject CreateTorus(double torusDiameter, double tubeDiameter, Point3D center, Color color)
    {
        var material = CreateShinyFrozenMaterial(color);
        var torus = new TorusVisual3D
        {
            TorusDiameter = torusDiameter,
            TubeDiameter = tubeDiameter,
            Material = material,
            Transform = new TranslateTransform3D(center.X, center.Y, center.Z)
        };
        return new HelixTorusObject(torus);
    }

    /// <inheritdoc/>
    public void AddObject(ISceneObject obj)
    {
        if (obj is HelixSceneObject helixObj)
        {
            _objects[obj.Id] = obj;
            _viewport.Children.Add(helixObj.Visual);
            CacheBounds(obj);
        }
    }

    /// <inheritdoc/>
    public void RemoveObject(ISceneObject obj)
    {
        if (obj is HelixSceneObject helixObj && _objects.Remove(obj.Id))
        {
            _viewport.Children.Remove(helixObj.Visual);
            _boundsCache.Remove(obj.Id);
        }
    }

    /// <inheritdoc/>
    public void ClearObjects()
    {
        foreach (var obj in _objects.Values.ToList())
        {
            RemoveObject(obj);
        }
        _objects.Clear();
        _boundsCache.Clear();
    }

    /// <inheritdoc/>
    public void UpdateObjectPosition(ISceneObject obj, Point3D newPosition)
    {
        obj.Position = newPosition;
    }

    #endregion

    #region Selection

    /// <inheritdoc/>
    public void SetSelection(ISceneObject? obj)
    {
        if (obj is null)
        {
            _selectionBox.BoundingBox = Rect3D.Empty;
        }
        else
        {
            UpdateSelectionBounds(GetObjectBounds(obj));
        }
    }

    /// <inheritdoc/>
    public void UpdateSelectionBounds(Rect3D bounds)
    {
        if (bounds.IsEmpty)
        {
            _selectionBox.BoundingBox = Rect3D.Empty;
        }
        else
        {
            var expanded = new Rect3D(
                bounds.X - 0.1,
                bounds.Y - 0.1,
                bounds.Z - 0.1,
                bounds.SizeX + 0.2,
                bounds.SizeY + 0.2,
                bounds.SizeZ + 0.2);
            _selectionBox.BoundingBox = expanded;
        }
    }

    #endregion

    #region Lighting

    /// <inheritdoc/>
    public void UpdateLights(IEnumerable<LightSource> lightSources)
    {
        // Remove existing lights
        foreach (var visual in _lightVisuals.Values)
            _viewport.Children.Remove(visual);
        _lightVisuals.Clear();

        foreach (var fixture in _lampFixtures.Values)
            _viewport.Children.Remove(fixture);
        _lampFixtures.Clear();

        // Add new lights
        foreach (var light in lightSources)
        {
            var lightVisual = CreateLightVisual(light);
            _lightVisuals[light] = lightVisual;
            _viewport.Children.Add(lightVisual);

            var fixture = CreateLampFixtureVisual(light);
            _lampFixtures[light] = fixture;
            _viewport.Children.Add(fixture);
        }
    }

    private static ModelVisual3D CreateLightVisual(LightSource light)
    {
        var directionalLight = new DirectionalLight
        {
            Color = light.EffectiveColor,
            Direction = light.Direction
        };

        return new ModelVisual3D
        {
            Content = directionalLight,
            Transform = Transform3D.Identity
        };
    }

    private static ModelVisual3D CreateLampFixtureVisual(LightSource light)
    {
        var fixtureGroup = new ModelVisual3D();

        var housingMaterial = CreateFrozenMaterial(Color.FromRgb(40, 40, 40));
        var housing = new PipeVisual3D
        {
            Point1 = new Point3D(0, 0, -0.3),
            Point2 = new Point3D(0, 0, 0),
            Diameter = 0.4,
            InnerDiameter = 0,
            Material = housingMaterial,
            Transform = Transform3D.Identity
        };

        var coneColor = Color.FromArgb(140, light.Color.R, light.Color.G, light.Color.B);
        var coneMaterial = CreateFrozenMaterial(coneColor);
        var lightCone = new TruncatedConeVisual3D
        {
            Origin = new Point3D(0, 0, 0),
            Height = 1.5,
            BaseRadius = 0.15,
            TopRadius = 0.6,
            Material = coneMaterial,
            Transform = Transform3D.Identity
        };

        var glowColor = Color.FromArgb(220,
            (byte)Math.Min(255, light.Color.R + 50),
            (byte)Math.Min(255, light.Color.G + 50),
            (byte)Math.Min(255, light.Color.B + 50));
        var glowMaterial = new EmissiveMaterial(new SolidColorBrush(glowColor));
        glowMaterial.Freeze();
        var bulb = new SphereVisual3D
        {
            Center = new Point3D(0, 0, 0.05),
            Radius = 0.12,
            Material = glowMaterial,
            Transform = Transform3D.Identity
        };

        var bracketMaterial = CreateFrozenMaterial(Color.FromRgb(60, 60, 60));
        var bracket = new PipeVisual3D
        {
            Point1 = new Point3D(0, 0, -0.3),
            Point2 = new Point3D(0, 0, -0.6),
            Diameter = 0.1,
            InnerDiameter = 0,
            Material = bracketMaterial,
            Transform = Transform3D.Identity
        };

        fixtureGroup.Children.Add(housing);
        fixtureGroup.Children.Add(lightCone);
        fixtureGroup.Children.Add(bulb);
        fixtureGroup.Children.Add(bracket);
        fixtureGroup.Transform = CreateLampTransform(light);

        return fixtureGroup;
    }

    private static Transform3D CreateLampTransform(LightSource light)
    {
        var transformGroup = new Transform3DGroup();
        var direction = light.Direction;
        direction.Normalize();

        var defaultDir = new Vector3D(0, 0, 1);
        var dot = Vector3D.DotProduct(defaultDir, direction);

        if (Math.Abs(dot + 1) < 0.0001)
        {
            transformGroup.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(1, 0, 0), 180)));
        }
        else if (Math.Abs(dot - 1) > 0.0001)
        {
            var rotationAxis = Vector3D.CrossProduct(defaultDir, direction);
            rotationAxis.Normalize();
            var angle = Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
            transformGroup.Children.Add(new RotateTransform3D(
                new AxisAngleRotation3D(rotationAxis, angle)));
        }

        transformGroup.Children.Add(new TranslateTransform3D(
            light.PositionX, light.PositionY, light.PositionZ));

        return transformGroup;
    }

    #endregion

    #region Hit Testing

    /// <inheritdoc/>
    public (ISceneObject? Object, Point3D Position)? HitTest(Point position)
    {
        try
        {
            var hits = _viewport.Viewport.FindHits(position);
            foreach (var hit in hits)
            {
                var visual = hit.Visual;
                while (visual is not null)
                {
                    var obj = _objects.Values
                        .OfType<HelixSceneObject>()
                        .FirstOrDefault(o => o.Visual == visual);

                    if (obj is not null)
                        return (obj, hit.Position);

                    visual = GetParentVisual3D(visual);
                }
            }
        }
        catch (ArgumentException)
        {
            // Handle null transform case
        }

        return null;
    }

    /// <inheritdoc/>
    public (Point3D Origin, Vector3D Direction)? GetRay(Point position)
    {
        var ray = _viewport.Viewport.GetRay(position);
        if (ray is null) return null;
        return (ray.Origin, ray.Direction);
    }

    private static Visual3D? GetParentVisual3D(Visual3D visual)
    {
        if (visual is ModelVisual3D modelVisual &&
            System.Windows.Media.VisualTreeHelper.GetParent(modelVisual) is Visual3D parent)
        {
            return parent;
        }
        return null;
    }

    #endregion

    #region Scene Management

    /// <inheritdoc/>
    public Rect3D GetObjectBounds(ISceneObject obj)
    {
        if (obj is HelixSceneObject helixObj)
        {
            return Visual3DHelper.FindBounds(helixObj.Visual, Transform3D.Identity);
        }
        return Rect3D.Empty;
    }

    /// <inheritdoc/>
    public void CaptureMouse() => _viewport.CaptureMouse();

    /// <inheritdoc/>
    public void ReleaseMouseCapture() => _viewport.ReleaseMouseCapture();

    private void CacheBounds(ISceneObject obj)
    {
        _boundsCache[obj.Id] = (obj.BoundingRadius, obj.BoundingRadius);
    }

    #endregion

    #region Helpers

    private static Material CreateFrozenMaterial(Color color)
    {
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        material.Freeze();
        return material;
    }

    private static Material CreateShinyFrozenMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        
        // Add diffuse component for base color
        var diffuse = new DiffuseMaterial(new SolidColorBrush(color));
        materialGroup.Children.Add(diffuse);
        
        // Add specular highlight for shininess (lower power = larger, more visible highlight)
        var specular = new SpecularMaterial(new SolidColorBrush(Colors.White), 40);
        materialGroup.Children.Add(specular);
        
        // Add subtle emissive component to ensure visibility on flat surfaces
        var emissiveColor = Color.FromArgb(30, color.R, color.G, color.B);
        var emissive = new EmissiveMaterial(new SolidColorBrush(emissiveColor));
        materialGroup.Children.Add(emissive);
        
        materialGroup.Freeze();
        return materialGroup;
    }

    #endregion
}
