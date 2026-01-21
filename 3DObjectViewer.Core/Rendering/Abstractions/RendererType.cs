namespace _3DObjectViewer.Core.Rendering.Abstractions;

/// <summary>
/// Identifies the available 3D rendering engines.
/// </summary>
public enum RendererType
{
    /// <summary>
    /// HelixToolkit.Wpf - Software-rendered WPF 3D using HelixToolkit.
    /// Good compatibility, moderate performance.
    /// </summary>
    HelixToolkitWpf,

    /// <summary>
    /// HelixToolkit.SharpDX - Hardware-accelerated DirectX 11 rendering.
    /// Best performance, requires DirectX 11 support.
    /// </summary>
    HelixToolkitSharpDX,

    /// <summary>
    /// Native WPF 3D - Uses built-in WPF Viewport3D without external libraries.
    /// Maximum compatibility, basic features.
    /// </summary>
    NativeWpf
}
