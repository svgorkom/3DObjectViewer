namespace _3DObjectViewer.Core.Models;

/// <summary>
/// Specifies the available material styles for 3D objects.
/// </summary>
/// <remarks>
/// Each material style provides a different visual appearance:
/// <list type="bullet">
///   <item><see cref="Shiny"/> - Standard plastic-like appearance with specular highlights</item>
///   <item><see cref="Metallic"/> - Highly reflective metallic surface</item>
///   <item><see cref="Matte"/> - Diffuse surface with no specular reflection</item>
///   <item><see cref="Glass"/> - Semi-transparent with high specular</item>
///   <item><see cref="Glowing"/> - Emissive material that appears to emit light</item>
///   <item><see cref="Neon"/> - Bright glowing effect with color bleeding</item>
///   <item><see cref="Random"/> - Randomly selected material style</item>
/// </list>
/// </remarks>
public enum MaterialStyle
{
    /// <summary>
    /// Standard shiny plastic-like material with specular highlights.
    /// </summary>
    Shiny,

    /// <summary>
    /// Highly reflective metallic surface with strong specular.
    /// </summary>
    Metallic,

    /// <summary>
    /// Diffuse matte surface with no specular reflection.
    /// </summary>
    Matte,

    /// <summary>
    /// Semi-transparent glass-like material.
    /// </summary>
    Glass,

    /// <summary>
    /// Emissive material that appears to glow.
    /// </summary>
    Glowing,

    /// <summary>
    /// Bright neon effect with strong emission.
    /// </summary>
    Neon,

    /// <summary>
    /// Randomly selected material style for variety.
    /// </summary>
    Random
}
