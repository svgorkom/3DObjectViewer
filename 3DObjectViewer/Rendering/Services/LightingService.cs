using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Models;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.Rendering.Services;

/// <summary>
/// Service responsible for managing light visuals in the 3D scene.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the creation and management of:
/// </para>
/// <list type="bullet">
///   <item><description>DirectionalLight visuals for scene illumination</description></item>
///   <item><description>Lamp fixture visuals showing light position and direction</description></item>
///   <item><description>Visual indicators (cones, bulbs) for light orientation</description></item>
/// </list>
/// </remarks>
public class LightingService
{
    private readonly HelixViewport3D _viewport;
    private readonly Dictionary<LightSource, ModelVisual3D> _lightVisualMap = [];
    private readonly Dictionary<LightSource, ModelVisual3D> _lampFixtureMap = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LightingService"/> class.
    /// </summary>
    /// <param name="viewport">The HelixViewport3D to manage lights in.</param>
    public LightingService(HelixViewport3D viewport)
    {
        _viewport = viewport;
    }

    /// <summary>
    /// Updates all light visuals in the viewport.
    /// </summary>
    /// <param name="lightSources">The collection of light sources.</param>
    public void UpdateAllLights(IEnumerable<LightSource> lightSources)
    {
        // Remove existing light visuals
        foreach (var lightVisual in _lightVisualMap.Values)
        {
            _viewport.Children.Remove(lightVisual);
        }
        _lightVisualMap.Clear();

        // Remove existing lamp fixtures
        foreach (var fixture in _lampFixtureMap.Values)
        {
            _viewport.Children.Remove(fixture);
        }
        _lampFixtureMap.Clear();

        // Create new light visuals and fixtures for each light source
        foreach (var light in lightSources)
        {
            var lightVisual = CreateLightVisual(light);
            _lightVisualMap[light] = lightVisual;
            _viewport.Children.Add(lightVisual);

            var fixtureVisual = CreateLampFixtureVisual(light);
            _lampFixtureMap[light] = fixtureVisual;
            _viewport.Children.Add(fixtureVisual);
        }
    }

    /// <summary>
    /// Clears all light visuals from the viewport.
    /// </summary>
    public void ClearAllLights()
    {
        foreach (var lightVisual in _lightVisualMap.Values)
        {
            _viewport.Children.Remove(lightVisual);
        }
        _lightVisualMap.Clear();

        foreach (var fixture in _lampFixtureMap.Values)
        {
            _viewport.Children.Remove(fixture);
        }
        _lampFixtureMap.Clear();
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

        // Lamp housing (dark cylinder body)
        var housingMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(40, 40, 40)));
        housingMaterial.Freeze();
        var housing = new PipeVisual3D
        {
            Point1 = new Point3D(0, 0, -0.3),
            Point2 = new Point3D(0, 0, 0),
            Diameter = 0.4,
            InnerDiameter = 0,
            Material = housingMaterial,
            Transform = Transform3D.Identity
        };

        // Light cone (shows direction with light color)
        var coneColor = Color.FromArgb(140, light.Color.R, light.Color.G, light.Color.B);
        var coneMaterial = new DiffuseMaterial(new SolidColorBrush(coneColor));
        coneMaterial.Freeze();

        var lightCone = new TruncatedConeVisual3D
        {
            Origin = new Point3D(0, 0, 0),
            Height = 1.5,
            BaseRadius = 0.15,
            TopRadius = 0.6,
            Material = coneMaterial,
            Transform = Transform3D.Identity
        };

        // Glowing bulb/lens at the front
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

        // Mount bracket
        var bracketMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(60, 60, 60)));
        bracketMaterial.Freeze();
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
}
