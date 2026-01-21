using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using _3DObjectViewer.Core.Rendering.Abstractions;

namespace _3DObjectViewer.Rendering;

/*
================================================================================
RENDERER MANAGER - RUNTIME RENDERING ENGINE SWITCHING
================================================================================

The RendererManager enables switching between different 3D rendering backends
at runtime without restarting the application. This supports scenarios like:
- User preference for different rendering quality/performance tradeoffs
- Fallback to software rendering if hardware acceleration unavailable
- A/B testing different rendering implementations

DESIGN PATTERNS:
- Strategy Pattern: IRenderer implementations are interchangeable strategies
- Observer Pattern: Events notify when renderer changes
- Facade Pattern: Provides simple interface to complex renderer switching

SWITCHING PROCESS:
1. User selects new renderer type (via UI binding to SelectedRendererType)
2. RendererChanging event fires (can be cancelled)
3. New renderer created and initialized
4. Camera settings transferred from old to new
5. RendererChanged event fires (subscribers recreate objects)
6. Old renderer disposed

EVENT HANDLING FOR RENDERER SWITCH:
Subscribers to RendererChanged should:
1. Unwire event handlers from OldRenderer.ViewportControl
2. Recreate scene objects using NewRenderer.CreateXxx() methods
3. Wire event handlers to NewRenderer.ViewportControl
4. Update UI to display NewRenderer.ViewportControl

THREAD SAFETY:
All operations must be called from the UI thread (WPF dispatcher).
================================================================================
*/

/// <summary>
/// Manages the active 3D renderer and handles runtime switching between rendering engines.
/// </summary>
/// <remarks>
/// <para>
/// The RendererManager is the central hub for renderer lifecycle management. It creates
/// the initial renderer, provides access to the current renderer, and orchestrates
/// the switching process when changing between rendering backends.
/// </para>
/// <para>
/// <b>For AI/LLM Context:</b>
/// <list type="bullet">
///   <item>Use <see cref="ActiveRenderer"/> to access current rendering operations</item>
///   <item>Set <see cref="ActiveRendererType"/> to trigger a renderer switch</item>
///   <item>Subscribe to <see cref="RendererChanged"/> to handle object recreation</item>
///   <item>Access <see cref="ViewportControl"/> for the WPF visual element</item>
/// </list>
/// </para>
/// <para>
/// <b>Typical Usage Flow:</b>
/// <code>
/// // 1. Create manager with default renderer
/// var manager = new RendererManager(RendererFactory.Instance);
/// 
/// // 2. Add viewport to UI
/// myContentControl.Content = manager.ViewportControl;
/// 
/// // 3. Handle renderer changes
/// manager.RendererChanged += (s, e) => {
///     myContentControl.Content = e.NewRenderer.ViewportControl;
///     // Recreate objects in new renderer...
/// };
/// 
/// // 4. Switch renderer (e.g., from combo box selection)
/// manager.ActiveRendererType = RendererType.HelixToolkitSharpDX;
/// </code>
/// </para>
/// </remarks>
public sealed class RendererManager : INotifyPropertyChanged
{
    private readonly IRendererFactory _factory;
    private IRenderer _activeRenderer;
    private RendererType _activeRendererType;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    /// <remarks>
    /// Properties that raise this event:
    /// <list type="bullet">
    ///   <item><see cref="ActiveRenderer"/> - after successful renderer switch</item>
    ///   <item><see cref="ActiveRendererType"/> - after successful renderer switch</item>
    ///   <item><see cref="ViewportControl"/> - after successful renderer switch</item>
    /// </list>
    /// </remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when the renderer is about to change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This event fires BEFORE the new renderer is created. Handlers can:
    /// <list type="bullet">
    ///   <item>Set <see cref="RendererChangingEventArgs.Cancel"/> = true to abort the switch</item>
    ///   <item>Save state that needs to be transferred to the new renderer</item>
    ///   <item>Show a loading indicator to the user</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Warning:</b> The old renderer is still active at this point. Do not dispose
    /// or modify it; that will be done after <see cref="RendererChanged"/> fires.
    /// </para>
    /// </remarks>
    public event EventHandler<RendererChangingEventArgs>? RendererChanging;

    /// <summary>
    /// Occurs after the renderer has changed successfully.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Critical:</b> Subscribers MUST recreate their scene objects in this handler
    /// because the old renderer's objects are no longer valid.
    /// </para>
    /// <para>
    /// Handler responsibilities:
    /// <list type="number">
    ///   <item>Unwire mouse/input events from <c>e.OldRenderer.ViewportControl</c></item>
    ///   <item>Recreate all scene objects via <c>e.NewRenderer.CreateXxx()</c></item>
    ///   <item>Recreate shadows and update lighting</item>
    ///   <item>Wire mouse/input events to <c>e.NewRenderer.ViewportControl</c></item>
    ///   <item>Update UI to display new viewport</item>
    /// </list>
    /// </para>
    /// <para>
    /// Camera position is automatically transferred before this event fires.
    /// </para>
    /// </remarks>
    public event EventHandler<RendererChangedEventArgs>? RendererChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="RendererManager"/> class.
    /// </summary>
    /// <param name="factory">The factory used to create renderer instances.</param>
    /// <param name="initialType">The initial renderer type to use. Defaults to HelixToolkit.Wpf.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown if the specified <paramref name="initialType"/> is not available.
    /// </exception>
    /// <remarks>
    /// The initial renderer is created and initialized immediately. The viewport
    /// is ready for use after construction.
    /// </remarks>
    public RendererManager(IRendererFactory factory, RendererType initialType = RendererType.HelixToolkitWpf)
    {
        _factory = factory;
        _activeRendererType = initialType;
        _activeRenderer = factory.CreateRenderer(initialType);
        _activeRenderer.Initialize();
    }

    /// <summary>
    /// Gets the currently active renderer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to access all rendering operations: creating objects, managing lights,
    /// performing hit tests, etc.
    /// </para>
    /// <para>
    /// <b>Warning:</b> This reference changes when <see cref="SwitchRenderer"/> is called.
    /// Do not cache this reference long-term; always access via this property.
    /// </para>
    /// </remarks>
    public IRenderer ActiveRenderer => _activeRenderer;

    /// <summary>
    /// Gets or sets the type of the currently active renderer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Setting this property triggers a renderer switch if:
    /// <list type="bullet">
    ///   <item>The value is different from the current type</item>
    ///   <item>The requested type is available (per <see cref="IRendererFactory.IsRendererAvailable"/>)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Bind this to a ComboBox for UI-driven renderer selection:
    /// <code>
    /// &lt;ComboBox ItemsSource="{Binding AvailableRenderers}"
    ///           SelectedItem="{Binding SelectedRendererType}"/&gt;
    /// </code>
    /// </para>
    /// </remarks>
    public RendererType ActiveRendererType
    {
        get => _activeRendererType;
        set
        {
            if (_activeRendererType != value && _factory.IsRendererAvailable(value))
            {
                SwitchRenderer(value);
            }
        }
    }

    /// <summary>
    /// Gets the WPF viewport control from the active renderer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the visual element that displays the 3D scene. Add it to your
    /// WPF visual tree (e.g., as Content of a ContentControl).
    /// </para>
    /// <para>
    /// The control type depends on the active renderer:
    /// <list type="bullet">
    ///   <item>HelixToolkit.Wpf: <c>HelixViewport3D</c></item>
    ///   <item>HelixToolkit.SharpDX: <c>Viewport3DX</c></item>
    ///   <item>Native WPF: <c>Viewport3D</c></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Important:</b> This property returns a new control after renderer switch.
    /// Subscribe to <see cref="RendererChanged"/> to update your UI.
    /// </para>
    /// </remarks>
    public FrameworkElement ViewportControl => _activeRenderer.ViewportControl;

    /// <summary>
    /// Gets the available renderer types on this system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns only renderers that are currently usable. A renderer may be unavailable if:
    /// <list type="bullet">
    ///   <item>Required NuGet package is not installed</item>
    ///   <item>Hardware requirements not met (e.g., DirectX version)</item>
    ///   <item>Implementation not yet completed</item>
    /// </list>
    /// </para>
    /// <para>
    /// Bind this to a ComboBox ItemsSource for UI-driven renderer selection.
    /// </para>
    /// </remarks>
    public IEnumerable<RendererType> AvailableRenderers => _factory.GetAvailableRenderers();

    /// <summary>
    /// Switches to a different renderer type.
    /// </summary>
    /// <param name="newType">The renderer type to switch to.</param>
    /// <returns>
    /// <c>true</c> if the switch was successful; <c>false</c> if it failed or was cancelled.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Switch Process:</b>
    /// <list type="number">
    ///   <item>Validate that <paramref name="newType"/> is available</item>
    ///   <item>Fire <see cref="RendererChanging"/> (can be cancelled)</item>
    ///   <item>Create and initialize new renderer</item>
    ///   <item>Transfer camera settings from old renderer</item>
    ///   <item>Update internal state to new renderer</item>
    ///   <item>Fire <see cref="RendererChanged"/> (subscribers recreate objects)</item>
    ///   <item>Dispose old renderer</item>
    ///   <item>Fire <see cref="PropertyChanged"/> for affected properties</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Failure handling:</b> If an exception occurs during switch, the original
    /// renderer remains active and false is returned.
    /// </para>
    /// </remarks>
    public bool SwitchRenderer(RendererType newType)
    {
        if (!_factory.IsRendererAvailable(newType))
            return false;

        if (_activeRendererType == newType)
            return true;

        var oldRenderer = _activeRenderer;
        var oldType = _activeRendererType;

        // Notify that we're about to change
        var changingArgs = new RendererChangingEventArgs(oldType, newType, oldRenderer);
        RendererChanging?.Invoke(this, changingArgs);

        if (changingArgs.Cancel)
            return false;

        try
        {
            // Create the new renderer
            var newRenderer = _factory.CreateRenderer(newType);
            newRenderer.Initialize();

            // Transfer camera settings
            newRenderer.CameraPosition = oldRenderer.CameraPosition;
            newRenderer.CameraLookDirection = oldRenderer.CameraLookDirection;
            newRenderer.CameraUpDirection = oldRenderer.CameraUpDirection;

            // Update state
            _activeRenderer = newRenderer;
            _activeRendererType = newType;

            // Notify that the change is complete (before disposing old renderer)
            var changedArgs = new RendererChangedEventArgs(oldType, newType, oldRenderer, newRenderer);
            RendererChanged?.Invoke(this, changedArgs);

            // Dispose old renderer after event handlers have finished
            oldRenderer.Dispose();

            OnPropertyChanged(nameof(ActiveRenderer));
            OnPropertyChanged(nameof(ActiveRendererType));
            OnPropertyChanged(nameof(ViewportControl));

            return true;
        }
        catch (Exception ex)
        {
            // Failed to switch - original renderer remains active
            System.Diagnostics.Debug.WriteLine($"Failed to switch renderer: {ex.Message}");
            return false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Event arguments for when the renderer is about to change.
/// </summary>
/// <remarks>
/// <para>
/// This event fires BEFORE the new renderer is created. The old renderer
/// is still active and can be queried for state that needs to be preserved.
/// </para>
/// <para>
/// Set <see cref="Cancel"/> to true to abort the renderer switch. This is useful
/// if there are unsaved changes or the user needs to confirm the action.
/// </para>
/// </remarks>
public sealed class RendererChangingEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RendererChangingEventArgs"/> class.
    /// </summary>
    /// <param name="oldType">The current renderer type being switched from.</param>
    /// <param name="newType">The requested new renderer type.</param>
    /// <param name="oldRenderer">The current renderer instance.</param>
    public RendererChangingEventArgs(RendererType oldType, RendererType newType, IRenderer oldRenderer)
    {
        OldType = oldType;
        NewType = newType;
        OldRenderer = oldRenderer;
    }

    /// <summary>
    /// Gets the renderer type being switched from.
    /// </summary>
    public RendererType OldType { get; }

    /// <summary>
    /// Gets the renderer type being switched to.
    /// </summary>
    public RendererType NewType { get; }

    /// <summary>
    /// Gets the current renderer instance (still active at this point).
    /// </summary>
    /// <remarks>
    /// Use this to query current state like camera position, selected objects, etc.
    /// Do not dispose or significantly modify this renderer.
    /// </remarks>
    public IRenderer OldRenderer { get; }

    /// <summary>
    /// Gets or sets whether to cancel the renderer switch.
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> to abort the switch. The current renderer will remain active
    /// and <see cref="RendererManager.RendererChanged"/> will not fire.
    /// </remarks>
    public bool Cancel { get; set; }
}

/// <summary>
/// Event arguments for when the renderer has changed successfully.
/// </summary>
/// <remarks>
/// <para>
/// This event fires AFTER the new renderer is created and initialized, but
/// BEFORE the old renderer is disposed. Both renderers are accessible.
/// </para>
/// <para>
/// <b>Critical:</b> Handlers must recreate all scene objects in the new renderer
/// because objects from the old renderer are no longer valid.
/// </para>
/// </remarks>
public sealed class RendererChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RendererChangedEventArgs"/> class.
    /// </summary>
    /// <param name="oldType">The previous renderer type.</param>
    /// <param name="newType">The new active renderer type.</param>
    /// <param name="oldRenderer">The previous renderer (about to be disposed).</param>
    /// <param name="newRenderer">The new active renderer.</param>
    public RendererChangedEventArgs(RendererType oldType, RendererType newType, IRenderer oldRenderer, IRenderer newRenderer)
    {
        OldType = oldType;
        NewType = newType;
        OldRenderer = oldRenderer;
        NewRenderer = newRenderer;
    }

    /// <summary>
    /// Gets the previous renderer type.
    /// </summary>
    public RendererType OldType { get; }

    /// <summary>
    /// Gets the new active renderer type.
    /// </summary>
    public RendererType NewType { get; }

    /// <summary>
    /// Gets the previous renderer instance (about to be disposed).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to:
    /// <list type="bullet">
    ///   <item>Unwire event handlers from <c>OldRenderer.ViewportControl</c></item>
    ///   <item>Copy state that wasn't automatically transferred</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Warning:</b> This renderer will be disposed after all event handlers complete.
    /// Do not store references to it.
    /// </para>
    /// </remarks>
    public IRenderer OldRenderer { get; }

    /// <summary>
    /// Gets the new active renderer instance.
    /// </summary>
    /// <remarks>
    /// Use this to:
    /// <list type="bullet">
    ///   <item>Recreate scene objects via <c>NewRenderer.CreateXxx()</c></item>
    ///   <item>Wire event handlers to <c>NewRenderer.ViewportControl</c></item>
    ///   <item>Update UI to display new viewport</item>
    /// </list>
    /// </remarks>
    public IRenderer NewRenderer { get; }
}
