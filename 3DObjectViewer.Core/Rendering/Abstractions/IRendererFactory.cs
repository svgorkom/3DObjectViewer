namespace _3DObjectViewer.Core.Rendering.Abstractions;

/// <summary>
/// Factory for creating renderer instances.
/// </summary>
public interface IRendererFactory
{
    /// <summary>
    /// Creates a renderer of the specified type.
    /// </summary>
    /// <param name="type">The type of renderer to create.</param>
    /// <returns>A new renderer instance.</returns>
    IRenderer CreateRenderer(RendererType type);

    /// <summary>
    /// Gets the available renderer types on this system.
    /// </summary>
    IEnumerable<RendererType> GetAvailableRenderers();

    /// <summary>
    /// Checks if a specific renderer type is available.
    /// </summary>
    bool IsRendererAvailable(RendererType type);
}
