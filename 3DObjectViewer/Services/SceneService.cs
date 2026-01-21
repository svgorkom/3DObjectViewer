using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Models;
using _3DObjectViewer.Rendering.Services;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.Services;

/// <summary>
/// Facade service that coordinates lighting management in the 3D scene.
/// </summary>
/// <remarks>
/// <para>
/// This service acts as a facade, delegating to specialized services:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="LightingService"/> for light visual management</description></item>
/// </list>
/// <para>
/// Use this service for simple scenarios. For more control, use the specialized services directly.
/// </para>
/// </remarks>
public class SceneService
{
    private readonly LightingService _lightingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SceneService"/> class.
    /// </summary>
    /// <param name="viewport">The HelixViewport3D to manage.</param>
    public SceneService(HelixViewport3D viewport)
    {
        _lightingService = new LightingService(viewport);
    }

    /// <summary>
    /// Gets the lighting service for direct access.
    /// </summary>
    public LightingService Lighting => _lightingService;

    #region Light Management

    /// <summary>
    /// Updates all light visuals in the viewport.
    /// </summary>
    /// <param name="lightSources">The collection of light sources.</param>
    public void UpdateAllLights(IEnumerable<LightSource> lightSources)
    {
        var lightList = lightSources.ToList();
        _lightingService.UpdateAllLights(lightList);
    }

    #endregion
}
