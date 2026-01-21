using _3DObjectViewer.Core.Rendering.Abstractions;
using _3DObjectViewer.Rendering.HelixWpf;

namespace _3DObjectViewer.Rendering;

/// <summary>
/// Factory for creating renderer instances.
/// </summary>
public sealed class RendererFactory : IRendererFactory
{
    private static readonly Lazy<RendererFactory> _instance = new(() => new RendererFactory());

    /// <summary>
    /// Gets the singleton instance of the renderer factory.
    /// </summary>
    public static RendererFactory Instance => _instance.Value;

    private RendererFactory() { }

    /// <inheritdoc/>
    public IRenderer CreateRenderer(RendererType type)
    {
        return type switch
        {
            RendererType.HelixToolkitWpf => new HelixWpfRenderer(),
            RendererType.HelixToolkitSharpDX => throw new NotSupportedException(
                "HelixToolkit.SharpDX is not yet implemented. Install the HelixToolkit.Wpf.SharpDX package and implement SharpDXRenderer."),
            RendererType.NativeWpf => throw new NotSupportedException(
                "Native WPF renderer is not yet implemented."),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown renderer type")
        };
    }

    /// <inheritdoc/>
    public IEnumerable<RendererType> GetAvailableRenderers()
    {
        // Check which renderers are available
        var available = new List<RendererType>();

        // HelixToolkit.Wpf is always available (it's our current dependency)
        available.Add(RendererType.HelixToolkitWpf);

        // Check for SharpDX support
        if (IsSharpDXAvailable())
        {
            available.Add(RendererType.HelixToolkitSharpDX);
        }

        // Native WPF is always available
        // available.Add(RendererType.NativeWpf);

        return available;
    }

    /// <inheritdoc/>
    public bool IsRendererAvailable(RendererType type)
    {
        return type switch
        {
            RendererType.HelixToolkitWpf => true,
            RendererType.HelixToolkitSharpDX => IsSharpDXAvailable(),
            RendererType.NativeWpf => false, // Not implemented yet
            _ => false
        };
    }

    /// <summary>
    /// Checks if SharpDX/DirectX rendering is available.
    /// </summary>
    private static bool IsSharpDXAvailable()
    {
        try
        {
            // Try to load the SharpDX assembly
            var assembly = System.Reflection.Assembly.Load("HelixToolkit.Wpf.SharpDX");
            return assembly is not null;
        }
        catch
        {
            return false;
        }
    }
}
