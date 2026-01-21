namespace _3DObjectViewer.Core.Models;

/// <summary>
/// Specifies the available colors for 3D objects in the scene.
/// </summary>
public enum ObjectColor
{
    /// <summary>
    /// Red color (RGB: 255, 0, 0).
    /// </summary>
    Red,

    /// <summary>
    /// Green color (RGB: 0, 128, 0).
    /// </summary>
    Green,

    /// <summary>
    /// Blue color (RGB: 0, 0, 255).
    /// </summary>
    Blue,

    /// <summary>
    /// Yellow color (RGB: 255, 255, 0).
    /// </summary>
    Yellow,

    /// <summary>
    /// A randomly generated RGB color.
    /// </summary>
    Random
}
