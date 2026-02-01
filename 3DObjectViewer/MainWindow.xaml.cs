using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Helpers;
using _3DObjectViewer.ViewModels;
using HelixToolkit.Wpf;

namespace _3DObjectViewer;

/// <summary>
/// The main window of the 3D Object Viewer application.
/// </summary>
/// <remarks>
/// <para>
/// This window serves as the View in the MVVM pattern, coordinating between
/// the ViewModel and the 3D viewports.
/// </para>
/// <para>
/// The window provides three synchronized viewports:
/// <list type="bullet">
///   <item><description>Main viewport: Perspective view with full camera control</description></item>
///   <item><description>Top viewport: Orthographic view looking down the Z-axis</description></item>
///   <item><description>Sagittal viewport: Orthographic side view looking along the X-axis</description></item>
/// </list>
/// </para>
/// <para>
/// Secondary viewports share the same Model3D content as the main viewport to ensure
/// perfect synchronization of object positions and transforms.
/// </para>
/// <para>
/// <b>Threading:</b> Physics updates are received from the ViewModel already throttled
/// by the <see cref="Services.SceneUpdateCoordinator"/>. Secondary viewport synchronization
/// is done lazily on render frames to minimize per-update overhead.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly BoundingBoxVisual3D _selectionBox;
    private readonly BoundingBoxVisual3D _selectionBoxTop;
    private readonly BoundingBoxVisual3D _selectionBoxSagittal;
    private readonly ModelVisual3D _terrainVisual;
    
    // Container for shared scene objects in secondary viewports
    // These ModelVisual3D instances reference the same Model3D as the original objects
    private readonly Dictionary<Visual3D, ModelVisual3D> _topViewModels = [];
    private readonly Dictionary<Visual3D, ModelVisual3D> _sagittalViewModels = [];
    
    // Cache for last synced transforms to avoid unnecessary WPF property updates
    private readonly Dictionary<Visual3D, Transform3D> _lastSyncedTransforms = [];
    
    private bool _isDragging;
    private Point _lastMousePosition;
    private Point3D _dragStartPosition;
    private DragPlane3D? _dragPlane;

    // FPS timing fields
    private readonly Stopwatch _fpsStopwatch = new();
    private int _frameCount;
    private TimeSpan _lastFpsUpdate;
    private TimeSpan _lastStatsUpdate;
    private const double StatsUpdateIntervalMs = 250;
    
    private int _cachedTriangleCount;
    private int _lastObjectCount;
    
    // Dirty flag for secondary viewport sync - prevents unnecessary sync when no physics updates occurred
    private bool _secondaryViewportsDirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Create selection box indicators for all viewports
        _selectionBox = new BoundingBoxVisual3D 
        { 
            Diameter = 0.05,
            Transform = Transform3D.Identity
        };
        HelixViewport.Children.Add(_selectionBox);
        
        _selectionBoxTop = new BoundingBoxVisual3D 
        { 
            Diameter = 0.03,
            Transform = Transform3D.Identity
        };
        TopViewport.Children.Add(_selectionBoxTop);
        
        _selectionBoxSagittal = new BoundingBoxVisual3D 
        { 
            Diameter = 0.03,
            Transform = Transform3D.Identity
        };
        SagittalViewport.Children.Add(_selectionBoxSagittal);

        // Create terrain
        _terrainVisual = CreateTerrainVisual();
        HelixViewport.Children.Add(_terrainVisual);

        // Subscribe to ViewModel events
        _viewModel.ResetCameraRequested += OnResetCameraRequested;
        _viewModel.SceneObjects.CollectionChanged += OnSceneObjectsChanged;
        _viewModel.SelectionChanged += OnSelectionChanged;
        _viewModel.PhysicsUpdated += OnPhysicsUpdated;

        KeyDown += OnKeyDown;

        _fpsStopwatch.Start();
        CompositionTarget.Rendering += OnCompositionTargetRendering;
        
        Closed += OnWindowClosed;
    }

    #region Terrain Creation

    /// <summary>
    /// Creates a flat terrain visual matching the physics ground plane.
    /// </summary>
    private static ModelVisual3D CreateTerrainVisual()
    {
        const int resolution = 40;
        const double size = 40.0;
        const double halfSize = size / 2;
        const double step = size / resolution;
        
        var positions = new Point3DCollection();
        var normals = new Vector3DCollection();
        var textureCoords = new PointCollection();
        var triangleIndices = new Int32Collection();
        
        for (int i = 0; i <= resolution; i++)
        {
            for (int j = 0; j <= resolution; j++)
            {
                double x = -halfSize + i * step;
                double y = -halfSize + j * step;
                double z = 0;
                
                positions.Add(new Point3D(x, y, z));
                textureCoords.Add(new Point((double)i / resolution, (double)j / resolution));
                normals.Add(new Vector3D(0, 0, 1));
            }
        }
        
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int topLeft = i * (resolution + 1) + j;
                int topRight = topLeft + 1;
                int bottomLeft = (i + 1) * (resolution + 1) + j;
                int bottomRight = bottomLeft + 1;
                
                triangleIndices.Add(topLeft);
                triangleIndices.Add(bottomLeft);
                triangleIndices.Add(topRight);
                
                triangleIndices.Add(topRight);
                triangleIndices.Add(bottomLeft);
                triangleIndices.Add(bottomRight);
            }
        }
        
        var mesh = new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            TextureCoordinates = textureCoords,
            TriangleIndices = triangleIndices
        };
        mesh.Freeze();
        
        var grassBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        grassBrush.GradientStops.Add(new GradientStop(Color.FromRgb(74, 124, 47), 0));
        grassBrush.GradientStops.Add(new GradientStop(Color.FromRgb(93, 138, 58), 0.3));
        grassBrush.GradientStops.Add(new GradientStop(Color.FromRgb(61, 107, 37), 0.7));
        grassBrush.GradientStops.Add(new GradientStop(Color.FromRgb(74, 124, 47), 1));
        grassBrush.Freeze();
        
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(grassBrush));
        material.Freeze();
        
        var geometry = new GeometryModel3D
        {
            Geometry = mesh,
            Material = material,
            BackMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(61, 40, 23)))
        };
        geometry.Freeze();
        
        return new ModelVisual3D { Content = geometry };
    }

    #endregion

    #region Secondary Viewport Synchronization

    /// <summary>
    /// Extracts the Model3D from a Visual3D.
    /// </summary>
    private static Model3D? ExtractModel(Visual3D visual)
    {
        if (visual is MeshElement3D meshElement)
        {
            return meshElement.Model;
        }
        if (visual is ModelVisual3D modelVisual)
        {
            return modelVisual.Content;
        }
        return null;
    }

    /// <summary>
    /// Synchronizes the transforms of secondary viewport models with their originals.
    /// Only syncs if the dirty flag is set and transforms have actually changed.
    /// </summary>
    private void SyncSecondaryViewportTransforms()
    {
        // Early exit if nothing changed
        if (!_secondaryViewportsDirty)
            return;
            
        _secondaryViewportsDirty = false;

        // Sync transforms only when they've actually changed
        foreach (var (original, topModelVisual) in _topViewModels)
        {
            var currentTransform = original.Transform;
            
            // Fast path: check if transform reference is the same as last synced
            if (_lastSyncedTransforms.TryGetValue(original, out var lastTransform) &&
                ReferenceEquals(currentTransform, lastTransform))
            {
                continue;
            }
            
            // Transform has changed - update both secondary viewports
            topModelVisual.Transform = currentTransform;
            
            if (_sagittalViewModels.TryGetValue(original, out var sagittalModelVisual))
            {
                sagittalModelVisual.Transform = currentTransform;
            }
            
            // Cache the new transform
            _lastSyncedTransforms[original] = currentTransform;
        }
    }

    /// <summary>
    /// Adds a visual's model to the secondary viewports.
    /// </summary>
    private void AddToSecondaryViewports(Visual3D visual)
    {
        var model = ExtractModel(visual);
        if (model is null) return;
        
        var transform = visual.Transform;
        
        var topModelVisual = new ModelVisual3D
        {
            Content = model,
            Transform = transform
        };
        _topViewModels[visual] = topModelVisual;
        TopViewport.Children.Add(topModelVisual);
        
        var sagittalModelVisual = new ModelVisual3D
        {
            Content = model,
            Transform = transform
        };
        _sagittalViewModels[visual] = sagittalModelVisual;
        SagittalViewport.Children.Add(sagittalModelVisual);
        
        // Cache the initial transform
        _lastSyncedTransforms[visual] = transform;
    }

    /// <summary>
    /// Removes a visual's model from the secondary viewports.
    /// </summary>
    private void RemoveFromSecondaryViewports(Visual3D visual)
    {
        if (_topViewModels.TryGetValue(visual, out var topModel))
        {
            TopViewport.Children.Remove(topModel);
            // Clear the Content reference to break the link to shared Model3D geometry
            topModel.Content = null;
            topModel.Transform = null;
            _topViewModels.Remove(visual);
        }
        
        if (_sagittalViewModels.TryGetValue(visual, out var sagittalModel))
        {
            SagittalViewport.Children.Remove(sagittalModel);
            // Clear the Content reference to break the link to shared Model3D geometry
            sagittalModel.Content = null;
            sagittalModel.Transform = null;
            _sagittalViewModels.Remove(visual);
        }
        
        // Clean up cached transform
        _lastSyncedTransforms.Remove(visual);
    }

    /// <summary>
    /// Clears all models from secondary viewports.
    /// </summary>
    private void ClearSecondaryViewports()
    {
        foreach (var model in _topViewModels.Values)
        {
            TopViewport.Children.Remove(model);
            // Clear content references to allow garbage collection
            model.Content = null;
            model.Transform = null;
        }
        _topViewModels.Clear();
        
        foreach (var model in _sagittalViewModels.Values)
        {
            SagittalViewport.Children.Remove(model);
            // Clear content references to allow garbage collection
            model.Content = null;
            model.Transform = null;
        }
        _sagittalViewModels.Clear();
        
        // Clear cached transforms
        _lastSyncedTransforms.Clear();
    }

    #endregion

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        
        _viewModel.ResetCameraRequested -= OnResetCameraRequested;
        _viewModel.SceneObjects.CollectionChanged -= OnSceneObjectsChanged;
        _viewModel.SelectionChanged -= OnSelectionChanged;
        _viewModel.PhysicsUpdated -= OnPhysicsUpdated;
        
        // Dispose the ViewModel (which disposes the physics engine and coordinator)
        _viewModel.Dispose();
        _fpsStopwatch.Stop();
    }

    #region Performance Statistics

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        _frameCount++;
        var elapsed = _fpsStopwatch.Elapsed;

        // Sync transforms only if dirty (set by physics update)
        SyncSecondaryViewportTransforms();

        if ((elapsed - _lastFpsUpdate).TotalMilliseconds >= 500)
        {
            var deltaSeconds = (elapsed - _lastFpsUpdate).TotalSeconds;
            var fps = _frameCount / deltaSeconds;
            var frameTimeMs = deltaSeconds / _frameCount * 1000;
            
            _viewModel.PerformanceStats.UpdateFps(fps, frameTimeMs);
            
            _frameCount = 0;
            _lastFpsUpdate = elapsed;
        }

        if ((elapsed - _lastStatsUpdate).TotalMilliseconds >= StatsUpdateIntervalMs)
        {
            UpdateStatistics();
            _lastStatsUpdate = elapsed;
        }
    }

    private void UpdateStatistics()
    {
        var objectCount = _viewModel.SceneObjects.Count;
        
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

    private int CountTriangles()
    {
        int totalTriangles = 0;
        foreach (var visual in _viewModel.SceneObjects)
        {
            totalTriangles += CountTrianglesInVisual(visual);
        }
        return totalTriangles;
    }

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

    private void OnPhysicsUpdated()
    {
        // Mark secondary viewports as dirty - they'll sync on next render frame
        _secondaryViewportsDirty = true;
        
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
                        AddToSecondaryViewports(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    foreach (Visual3D item in e.OldItems)
                    {
                        HelixViewport.Children.Remove(item);
                        RemoveFromSecondaryViewports(item);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                var itemsToRemove = HelixViewport.Children
                    .OfType<Visual3D>()
                    .Where(IsUserCreatedObject)
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    HelixViewport.Children.Remove(item);
                }
                
                ClearSecondaryViewports();
                break;
        }
    }

    private void OnResetCameraRequested()
    {
        HelixViewport.Camera.Position = new Point3D(15, 15, 12);
        HelixViewport.Camera.LookDirection = new Vector3D(-15, -15, -12);
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
            _selectionBoxTop.BoundingBox = Rect3D.Empty;
            _selectionBoxSagittal.BoundingBox = Rect3D.Empty;
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
            _selectionBoxTop.BoundingBox = expandedBounds;
            _selectionBoxSagittal.BoundingBox = expandedBounds;
        }
    }

    private static bool IsUserCreatedObject(Visual3D visual)
    {
        return visual is BoxVisual3D 
            or SphereVisual3D 
            or PipeVisual3D 
            or TruncatedConeVisual3D 
            or TorusVisual3D;
    }

    #endregion
}