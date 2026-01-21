using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Helpers;
using _3DObjectViewer.Core.Models;
using _3DObjectViewer.Services;
using _3DObjectViewer.ViewModels;
using HelixToolkit.Wpf;

namespace _3DObjectViewer;

/// <summary>
/// The main window of the 3D Object Viewer application.
/// </summary>
/// <remarks>
/// <para>
/// This window serves as the View in the MVVM pattern, coordinating between
/// the ViewModel and the 3D viewport. It delegates most rendering logic to
/// the <see cref="SceneService"/>.
/// </para>
/// <para>
/// The window handles view-specific concerns that cannot be easily moved to ViewModels:
/// </para>
/// <list type="bullet">
///   <item><description>Mouse hit-testing and drag operations (requires viewport access)</description></item>
///   <item><description>Selection box visual updates (requires Visual3D manipulation)</description></item>
///   <item><description>CompositionTarget.Rendering timing (UI thread requirement)</description></item>
///   <item><description>Triangle counting (requires traversing visual tree)</description></item>
/// </list>
/// <para>
/// Performance statistics are managed by <see cref="PerformanceStatsViewModel"/> and
/// displayed via data binding, with timing logic remaining in the View.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SceneService _sceneService;
    private readonly BoundingBoxVisual3D _selectionBox;
    
    private bool _isDragging;
    private Point _lastMousePosition;
    private Point3D _dragStartPosition;
    private DragPlane3D? _dragPlane;

    // FPS timing fields (kept in View for CompositionTarget.Rendering access)
    private readonly Stopwatch _fpsStopwatch = new();
    private int _frameCount;
    private TimeSpan _lastFpsUpdate;
    private TimeSpan _lastStatsUpdate;
    private const double StatsUpdateIntervalMs = 250;
    
    // Triangle count caching (expensive to calculate)
    private int _cachedTriangleCount;
    private int _lastObjectCount = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Initialize scene service
        _sceneService = new SceneService(HelixViewport);

        // Create selection box indicator
        _selectionBox = new BoundingBoxVisual3D 
        { 
            Diameter = 0.05,
            Transform = Transform3D.Identity
        };
        HelixViewport.Children.Add(_selectionBox);

        // Subscribe to ViewModel events
        _viewModel.ResetCameraRequested += OnResetCameraRequested;
        _viewModel.SceneObjects.CollectionChanged += OnSceneObjectsChanged;
        _viewModel.SelectionChanged += OnSelectionChanged;
        _viewModel.LightingChanged += OnLightingChanged;
        _viewModel.PhysicsUpdated += OnPhysicsUpdated;
        _viewModel.Lighting.LightSources.CollectionChanged += OnLightSourcesCollectionChanged;

        // Initialize lights
        _sceneService.UpdateAllLights(_viewModel.Lighting.LightSources);

        // Handle keyboard input
        KeyDown += OnKeyDown;

        // Start FPS counter and statistics
        _fpsStopwatch.Start();
        CompositionTarget.Rendering += OnCompositionTargetRendering;
        
        // Ensure cleanup when window closes
        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Handles light sources collection changes.
    /// </summary>
    private void OnLightSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnLightingChanged();
    }

    /// <summary>
    /// Cleans up event subscriptions when the window is closed to prevent memory leaks.
    /// </summary>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Unsubscribe from CompositionTarget.Rendering to prevent memory leak
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        
        // Unsubscribe from ViewModel events
        _viewModel.ResetCameraRequested -= OnResetCameraRequested;
        _viewModel.SceneObjects.CollectionChanged -= OnSceneObjectsChanged;
        _viewModel.SelectionChanged -= OnSelectionChanged;
        _viewModel.LightingChanged -= OnLightingChanged;
        _viewModel.PhysicsUpdated -= OnPhysicsUpdated;
        _viewModel.Lighting.LightSources.CollectionChanged -= OnLightSourcesCollectionChanged;
        
        // Stop physics engine
        _viewModel.PhysicsEngine.Stop();
        
        // Stop stopwatch
        _fpsStopwatch.Stop();
    }

    #region Performance Statistics

    /// <summary>
    /// Handles the CompositionTarget.Rendering event to calculate FPS and update statistics.
    /// </summary>
    /// <remarks>
    /// This method runs on every frame render and delegates to the PerformanceStatsViewModel
    /// for display updates while keeping timing logic in the View.
    /// </remarks>
    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        _frameCount++;
        var elapsed = _fpsStopwatch.Elapsed;

        // Update FPS display every 500ms for stable readings
        if ((elapsed - _lastFpsUpdate).TotalMilliseconds >= 500)
        {
            var deltaSeconds = (elapsed - _lastFpsUpdate).TotalSeconds;
            var fps = _frameCount / deltaSeconds;
            var frameTimeMs = deltaSeconds / _frameCount * 1000;
            
            _viewModel.PerformanceStats.UpdateFps(fps, frameTimeMs);
            
            _frameCount = 0;
            _lastFpsUpdate = elapsed;
        }

        // Update other statistics less frequently
        if ((elapsed - _lastStatsUpdate).TotalMilliseconds >= StatsUpdateIntervalMs)
        {
            UpdateStatistics();
            _lastStatsUpdate = elapsed;
        }
    }

    /// <summary>
    /// Collects and updates scene statistics via the PerformanceStatsViewModel.
    /// </summary>
    private void UpdateStatistics()
    {
        var objectCount = _viewModel.SceneObjects.Count;
        
        // Only recalculate triangles if object count changed (expensive operation)
        if (objectCount != _lastObjectCount)
        {
            _cachedTriangleCount = CountTriangles();
            _lastObjectCount = objectCount;
        }
        
        var bodyCount = _viewModel.PhysicsEngine.BodyCount;
        var activeBodyCount = _viewModel.PhysicsEngine.ActiveBodyCount;

        _viewModel.PerformanceStats.UpdateStatistics(
            objectCount, 
            _cachedTriangleCount, 
            bodyCount, 
            activeBodyCount,
            _viewModel.PhysicsEngineName);

        var camPos = HelixViewport.Camera.Position;
        _viewModel.PerformanceStats.UpdateCameraPosition(camPos.X, camPos.Y, camPos.Z);
    }

    /// <summary>
    /// Counts the total number of triangles in all scene objects.
    /// </summary>
    /// <returns>The total triangle count.</returns>
    private int CountTriangles()
    {
        int totalTriangles = 0;

        foreach (var visual in _viewModel.SceneObjects)
        {
            totalTriangles += CountTrianglesInVisual(visual);
        }

        return totalTriangles;
    }

    /// <summary>
    /// Recursively counts triangles in a Visual3D and its children.
    /// </summary>
    /// <param name="visual">The visual to count triangles in.</param>
    /// <returns>The triangle count for this visual and its descendants.</returns>
    private static int CountTrianglesInVisual(Visual3D visual)
    {
        int count = 0;

        if (visual is ModelVisual3D modelVisual && modelVisual.Content is not null)
        {
            count += CountTrianglesInModel(modelVisual.Content);
        }
        else if (visual is MeshElement3D meshElement)
        {
            var geometry = meshElement.Model?.Geometry as MeshGeometry3D;
            if (geometry?.TriangleIndices is not null)
            {
                count += geometry.TriangleIndices.Count / 3;
            }
            else if (geometry?.Positions is not null)
            {
                count += geometry.Positions.Count / 3;
            }
        }

        // Check children
        int childCount = VisualTreeHelper.GetChildrenCount(visual);
        for (int i = 0; i < childCount; i++)
        {
            if (VisualTreeHelper.GetChild(visual, i) is Visual3D childVisual)
            {
                count += CountTrianglesInVisual(childVisual);
            }
        }

        return count;
    }

    /// <summary>
    /// Counts triangles in a Model3D, recursively processing Model3DGroups.
    /// </summary>
    /// <param name="model">The model to count triangles in.</param>
    /// <returns>The triangle count for this model.</returns>
    private static int CountTrianglesInModel(Model3D model)
    {
        int count = 0;

        if (model is GeometryModel3D geometryModel)
        {
            if (geometryModel.Geometry is MeshGeometry3D mesh)
            {
                if (mesh.TriangleIndices is not null && mesh.TriangleIndices.Count > 0)
                {
                    count += mesh.TriangleIndices.Count / 3;
                }
                else if (mesh.Positions is not null)
                {
                    count += mesh.Positions.Count / 3;
                }
            }
        }
        else if (model is Model3DGroup group)
        {
            foreach (var child in group.Children)
            {
                count += CountTrianglesInModel(child);
            }
        }

        return count;
    }

    #endregion

    #region Event Handlers

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel.Selection.HasSelection)
        {
            _viewModel.Selection.DeselectCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _viewModel.Selection.HasSelection)
        {
            _viewModel.Selection.DeleteSelectedCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            // Toggle physics with spacebar
            _viewModel.TogglePhysicsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void HelixViewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(HelixViewport);

        IEnumerable<Viewport3DHelper.HitResult> hitResult;
        try
        {
            hitResult = HelixViewport.Viewport.FindHits(position);
        }
        catch (ArgumentException)
        {
            // Handle case where a Visual3D has null transform
            return;
        }

        foreach (var hit in hitResult)
        {
            var visual = hit.Visual;

            while (visual is not null)
            {
                if (_viewModel.SceneObjects.Contains(visual))
                {
                    _viewModel.Selection.SelectedObject = visual;

                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        StartDragging(position, hit.Position);
                        e.Handled = true;
                    }
                    return;
                }

                visual = GetParentVisual3D(visual);
            }
        }

        if (!_isDragging)
        {
            _viewModel.Selection.SelectedObject = null;
        }
    }

    private void HelixViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _viewModel.Selection.SelectedObject is null || _dragPlane is null)
            return;

        Point position = e.GetPosition(HelixViewport);
        Ray3D? ray = HelixViewport.Viewport.GetRay(position);
        if (ray is null)
            return;

        Point3D? intersection = ray.PlaneIntersection(_dragPlane.Position, _dragPlane.Normal);
        if (intersection.HasValue)
        {
            _viewModel.Selection.MoveSelectedObject(intersection.Value);
        }

        _lastMousePosition = position;
    }

    private void HelixViewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            StopDragging();
            e.Handled = true;
        }
    }

    private void OnSelectionChanged()
    {
        UpdateSelectionBox();
    }

    private void OnLightingChanged()
    {
        _sceneService.UpdateAllLights(_viewModel.Lighting.LightSources);
    }

    private void OnPhysicsUpdated()
    {
        // Update selection box if selected object is moving
        if (_viewModel.Selection.HasSelection)
        {
            UpdateSelectionBox();
        }
    }

    private void OnSceneObjectsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    foreach (Visual3D item in e.NewItems)
                    {
                        HelixViewport.Children.Add(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    foreach (Visual3D item in e.OldItems)
                    {
                        HelixViewport.Children.Remove(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // Only remove user-created objects, not scene infrastructure
                // (GridLinesVisual3D, RectangleVisual3D, CoordinateSystemVisual3D, etc.)
                // We identify user objects as specific primitive types that we create
                var itemsToRemove = HelixViewport.Children
                    .OfType<Visual3D>()
                    .Where(IsUserCreatedObject)
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    HelixViewport.Children.Remove(item);
                }
                break;
        }
    }

    private void OnResetCameraRequested()
    {
        HelixViewport.Camera.Position = new Point3D(10, 10, 10);
        HelixViewport.Camera.LookDirection = new Vector3D(-10, -10, -10);
        HelixViewport.Camera.UpDirection = new Vector3D(0, 0, 1);
    }

    #endregion

    #region Helper Methods

    private void StartDragging(Point mousePosition, Point3D hitPoint)
    {
        _isDragging = true;
        _lastMousePosition = mousePosition;
        _dragStartPosition = _viewModel.Selection.GetSelectedObjectPosition();

        Vector3D lookDirection = HelixViewport.Camera.LookDirection;
        lookDirection.Normalize();

        _dragPlane = new DragPlane3D(_dragStartPosition, lookDirection);
        HelixViewport.CaptureMouse();
    }

    private void StopDragging()
    {
        _isDragging = false;
        _dragPlane = null;
        HelixViewport.ReleaseMouseCapture();
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

    private void UpdateSelectionBox()
    {
        if (_viewModel.Selection.SelectedObject is null)
        {
            _selectionBox.BoundingBox = Rect3D.Empty;
            return;
        }

        Rect3D bounds = Visual3DHelper.FindBounds(_viewModel.Selection.SelectedObject, Transform3D.Identity);
        if (!bounds.IsEmpty)
        {
            var expandedBounds = new Rect3D(
                bounds.X - 0.1,
                bounds.Y - 0.1,
                bounds.Z - 0.1,
                bounds.SizeX + 0.2,
                bounds.SizeY + 0.2,
                bounds.SizeZ + 0.2);
            _selectionBox.BoundingBox = expandedBounds;
        }
    }

    /// <summary>
    /// Determines if a Visual3D is a user-created object (not scene infrastructure).
    /// </summary>
    /// <param name="visual">The visual to check.</param>
    /// <returns>True if the visual is a user-created object; otherwise, false.</returns>
    private static bool IsUserCreatedObject(Visual3D visual)
    {
        // User-created objects are the specific primitive types we add via ObjectsViewModel
        return visual is BoxVisual3D 
            or SphereVisual3D 
            or PipeVisual3D 
            or TruncatedConeVisual3D 
            or TorusVisual3D;
    }

    #endregion
}