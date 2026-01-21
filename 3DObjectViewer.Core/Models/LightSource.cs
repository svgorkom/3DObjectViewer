using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace _3DObjectViewer.Core.Models;

/*
================================================================================
LIGHT SOURCE MODEL
================================================================================

Represents a directional light source in the 3D scene. This model supports:
- Direction: Where the light rays point (for illumination calculations)
- Position: Where the light fixture is located (for visual representation)
- Color and Brightness: Combined to produce the effective illumination

LIGHT TYPES IN 3D GRAPHICS:
This implementation uses DIRECTIONAL lighting, which simulates light from
a very distant source (like the sun). All rays are parallel, regardless of
the light's position.

The Position property is used for:
1. Rendering a visual "lamp" fixture in the scene
2. Calculating shadow projection onto the ground plane
3. Providing UI controls for light placement

SHADOW CALCULATION:
Shadows are projected from objects onto the ground plane (Z=0) using
ray casting from the light Position through object centers.

DATA BINDING:
This class implements INotifyPropertyChanged for WPF data binding.
Individual color components (ColorR, ColorG, ColorB) are exposed
for slider controls in the UI.
================================================================================
*/

/// <summary>
/// Represents a directional light source in the 3D scene with position, direction, color, and brightness.
/// </summary>
/// <remarks>
/// <para>
/// A light source illuminates objects in the scene and casts shadows onto the ground plane.
/// The light uses a directional lighting model where all rays are parallel.
/// </para>
/// <para>
/// <b>For AI/LLM Context:</b>
/// <list type="bullet">
///   <item><see cref="Direction"/>: Vector pointing where light rays travel (not where light comes FROM)</item>
///   <item><see cref="Position"/>: Location of the visible lamp fixture (affects shadow projection)</item>
///   <item><see cref="EffectiveColor"/>: Use this for actual rendering (combines Color and Brightness)</item>
/// </list>
/// </para>
/// <para>
/// <b>MVVM Pattern:</b> This model implements <see cref="INotifyPropertyChanged"/> and is designed
/// for two-way data binding with WPF controls.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a white light pointing downward from above
/// var sunLight = new LightSource(
///     name: "Sun",
///     directionX: -1, directionY: -1, directionZ: -2,  // Points down and to the side
///     color: Colors.White,
///     brightness: 0.8,
///     positionX: 5, positionY: 5, positionZ: 10        // High up in the scene
/// );
/// </code>
/// </example>
public class LightSource : INotifyPropertyChanged
{
    private string _name;
    private double _directionX;
    private double _directionY;
    private double _directionZ;
    private double _positionX;
    private double _positionY;
    private double _positionZ;
    private Color _color;
    private double _brightness;

    /// <summary>
    /// Initializes a new instance of the <see cref="LightSource"/> class with default position.
    /// </summary>
    /// <param name="name">Display name shown in UI (e.g., "Sun", "Spotlight 1").</param>
    /// <param name="directionX">X component of the light direction vector.</param>
    /// <param name="directionY">Y component of the light direction vector.</param>
    /// <param name="directionZ">Z component of the light direction vector (negative = downward).</param>
    /// <param name="color">Base color of the light (before brightness is applied).</param>
    /// <param name="brightness">Intensity multiplier from 0.0 (off) to 1.0 (full).</param>
    /// <remarks>
    /// Default position is (0, 0, 8) - centered above the scene at height 8 units.
    /// </remarks>
    public LightSource(string name, double directionX, double directionY, double directionZ, Color color, double brightness)
        : this(name, directionX, directionY, directionZ, color, brightness, 0, 0, 8)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LightSource"/> class with explicit position.
    /// </summary>
    /// <param name="name">Display name shown in UI.</param>
    /// <param name="directionX">X component of the light direction vector.</param>
    /// <param name="directionY">Y component of the light direction vector.</param>
    /// <param name="directionZ">Z component of the light direction vector.</param>
    /// <param name="color">Base color of the light.</param>
    /// <param name="brightness">Intensity multiplier (0.0 to 1.0).</param>
    /// <param name="positionX">X coordinate of the light fixture position.</param>
    /// <param name="positionY">Y coordinate of the light fixture position.</param>
    /// <param name="positionZ">Z coordinate of the light fixture position (height above ground).</param>
    public LightSource(string name, double directionX, double directionY, double directionZ,
        Color color, double brightness, double positionX, double positionY, double positionZ)
    {
        _name = name;
        _directionX = directionX;
        _directionY = directionY;
        _directionZ = directionZ;
        _color = color;
        _brightness = brightness;
        _positionX = positionX;
        _positionY = positionY;
        _positionZ = positionZ;
    }

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the display name of the light.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    #region Direction Properties

    /// <summary>
    /// Gets or sets the X component of the light direction vector.
    /// </summary>
    public double DirectionX
    {
        get => _directionX;
        set => SetProperty(ref _directionX, value);
    }

    /// <summary>
    /// Gets or sets the Y component of the light direction vector.
    /// </summary>
    public double DirectionY
    {
        get => _directionY;
        set => SetProperty(ref _directionY, value);
    }

    /// <summary>
    /// Gets or sets the Z component of the light direction vector.
    /// </summary>
    public double DirectionZ
    {
        get => _directionZ;
        set => SetProperty(ref _directionZ, value);
    }

    /// <summary>
    /// Gets the light direction as a normalized Vector3D.
    /// </summary>
    public Vector3D Direction => new(DirectionX, DirectionY, DirectionZ);

    #endregion

    #region Position Properties

    /// <summary>
    /// Gets or sets the X position of the light fixture.
    /// </summary>
    public double PositionX
    {
        get => _positionX;
        set => SetProperty(ref _positionX, value);
    }

    /// <summary>
    /// Gets or sets the Y position of the light fixture.
    /// </summary>
    public double PositionY
    {
        get => _positionY;
        set => SetProperty(ref _positionY, value);
    }

    /// <summary>
    /// Gets or sets the Z position (height) of the light fixture.
    /// </summary>
    public double PositionZ
    {
        get => _positionZ;
        set => SetProperty(ref _positionZ, value);
    }

    /// <summary>
    /// Gets the light fixture position as a Point3D.
    /// </summary>
    public Point3D Position => new(PositionX, PositionY, PositionZ);

    #endregion

    #region Color Properties

    /// <summary>
    /// Gets or sets the base color of the light.
    /// </summary>
    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    /// <summary>
    /// Gets or sets the red component of the light color (0-255).
    /// </summary>
    public byte ColorR
    {
        get => _color.R;
        set
        {
            _color = Color.FromRgb(value, _color.G, _color.B);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(EffectiveColor));
        }
    }

    /// <summary>
    /// Gets or sets the green component of the light color (0-255).
    /// </summary>
    public byte ColorG
    {
        get => _color.G;
        set
        {
            _color = Color.FromRgb(_color.R, value, _color.B);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(EffectiveColor));
        }
    }

    /// <summary>
    /// Gets or sets the blue component of the light color (0-255).
    /// </summary>
    public byte ColorB
    {
        get => _color.B;
        set
        {
            _color = Color.FromRgb(_color.R, _color.G, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(EffectiveColor));
        }
    }

    /// <summary>
    /// Gets or sets the brightness/intensity of the light (0.0 to 1.0).
    /// </summary>
    public double Brightness
    {
        get => _brightness;
        set
        {
            if (SetProperty(ref _brightness, Math.Clamp(value, 0.0, 1.0)))
            {
                OnPropertyChanged(nameof(EffectiveColor));
            }
        }
    }

    /// <summary>
    /// Gets the effective color with brightness applied.
    /// </summary>
    public Color EffectiveColor => Color.FromRgb(
        (byte)(Color.R * Brightness),
        (byte)(Color.G * Brightness),
        (byte)(Color.B * Brightness));

    #endregion

    /// <summary>
    /// Returns the display name of the light.
    /// </summary>
    public override string ToString() => Name;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
