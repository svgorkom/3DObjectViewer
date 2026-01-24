using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Infrastructure;
using _3DObjectViewer.Core.Physics;
using _3DObjectViewer.Physics;
using _3DObjectViewer.Services;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// Main ViewModel that orchestrates the 3D Object Viewer application.
/// </summary>
/// <remarks>
/// <para>
/// This ViewModel composes specialized child ViewModels for objects, selection, and settings,
/// providing a unified interface for the main window.
/// </para>
/// <para>
/// Physics updates are coordinated through <see cref="SceneUpdateCoordinator"/> to
/// prevent UI thread blocking when many objects are moving.
/// </para>
/// </remarks>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly Dictionary<Visual3D, Guid> _visualToBodyId = [];
    private readonly Random _random = new();
    private readonly SceneUpdateCoordinator _updateCoordinator;
    private readonly ThemeService _themeService;
    
    private bool _physicsEnabled = true;
    private double _gravity = 9.81;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance using the default threaded physics engine.
    /// </summary>
    public MainViewModel() : this(PhysicsEngineType.BepuThreaded)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified physics engine type.
    /// </summary>
    public MainViewModel(PhysicsEngineType physicsEngineType)
    {
        SceneObjects = [];
        PhysicsEngine = new PhysicsEngine(physicsEngineType);

        // Create coordinator for throttled UI updates
        _updateCoordinator = new SceneUpdateCoordinator();
        _updateCoordinator.BatchedUpdateReady += OnBatchedPhysicsUpdate;

        // Create theme service
        _themeService = new ThemeService();
        _themeService.Initialize();

        // Create child ViewModels
        Objects = new ObjectsViewModel(SceneObjects);
        Selection = new SelectionViewModel(SceneObjects, Objects);
        PerformanceStats = new PerformanceStatsViewModel();
        Settings = new SettingsViewModel(_themeService);

        // Commands
        ClearAllCommand = new RelayCommand(ClearAll, () => SceneObjects.Count > 0);
        ResetCameraCommand = new RelayCommand(() => ResetCameraRequested?.Invoke());
        TogglePhysicsCommand = new RelayCommand(TogglePhysics);
        DropAllCommand = new RelayCommand(DropAll, () => SceneObjects.Count > 0);

        // Wire up events
        Selection.SelectionChanged += () => SelectionChanged?.Invoke();
        Selection.ObjectDeleted += OnObjectDeleted;
        Selection.ObjectMoved += OnObjectMoved;
        Objects.ObjectAdded += OnObjectAdded;

        // Configure physics
        PhysicsEngine.Gravity = Gravity;
        PhysicsEngine.BodiesUpdated += OnPhysicsBodiesUpdated;
        PhysicsEngine.SetBucketBounds(-10, 10, -10, 10, 4);

        if (PhysicsEnabled)
        {
            PhysicsEngine.Start();
        }
    }

    #region Events

    /// <summary>Raised when camera reset is requested.</summary>
    public event Action? ResetCameraRequested;

    /// <summary>Raised when selection visual needs update.</summary>
    public event Action? SelectionChanged;

    /// <summary>Raised when physics updates object positions (throttled).</summary>
    public event Action? PhysicsUpdated;

    #endregion

    #region Child ViewModels

    public ObjectsViewModel Objects { get; }
    public SelectionViewModel Selection { get; }
    public PerformanceStatsViewModel PerformanceStats { get; }
    public SettingsViewModel Settings { get; }
    public PhysicsEngine PhysicsEngine { get; }

    #endregion

    #region Properties

    public ObservableCollection<Visual3D> SceneObjects { get; }

    public string PhysicsEngineName => PhysicsEngine.Name;

    public bool PhysicsEnabled
    {
        get => _physicsEnabled;
        set
        {
            if (SetProperty(ref _physicsEnabled, value))
            {
                if (value) PhysicsEngine.Start();
                else PhysicsEngine.Stop();
            }
        }
    }

    public double Gravity
    {
        get => _gravity;
        set
        {
            if (SetProperty(ref _gravity, value))
            {
                PhysicsEngine.Gravity = value;
            }
        }
    }

    public bool HasSelection => Selection.HasSelection;

    #endregion

    #region Commands

    public ICommand ClearAllCommand { get; }
    public ICommand ResetCameraCommand { get; }
    public ICommand TogglePhysicsCommand { get; }
    public ICommand DropAllCommand { get; }

    #endregion

    #region Private Methods

    private void ClearAll()
    {
        Selection.SelectedObject = null;
        PhysicsEngine.Clear();
        PhysicsHelper.ClearVisualCache();
        _visualToBodyId.Clear();
        SceneObjects.Clear();
    }

    private void TogglePhysics() => PhysicsEnabled = !PhysicsEnabled;

    private void DropAll() => PhysicsEngine.WakeAllBodies();

    private void OnObjectAdded(Visual3D visual, Point3D position)
    {
        var body = PhysicsHelper.CreateRigidBody(visual, position);
        
        // Add random horizontal velocity for visual interest
        body.Velocity = new Vector3D(
            (_random.NextDouble() - 0.5) * 2.0,
            (_random.NextDouble() - 0.5) * 2.0,
            0);
        
        PhysicsEngine.AddBody(body);
        _visualToBodyId[visual] = body.Id;
    }

    private void OnPhysicsBodiesUpdated(IReadOnlyList<RigidBody> bodies)
    {
        _updateCoordinator.QueuePhysicsUpdate(bodies);
    }

    private void OnBatchedPhysicsUpdate(IReadOnlyList<RigidBody> bodies)
    {
        for (int i = 0; i < bodies.Count; i++)
        {
            PhysicsHelper.ApplyTransformToVisual(bodies[i]);
        }
        PhysicsUpdated?.Invoke();
    }

    private void OnObjectDeleted(Visual3D visual)
    {
        if (_visualToBodyId.TryGetValue(visual, out var bodyId))
        {
            PhysicsEngine.RemoveBodyById(bodyId);
            PhysicsHelper.RemoveVisualCache(bodyId);
            _visualToBodyId.Remove(visual);
        }
    }

    private void OnObjectMoved(Visual3D visual, Point3D newPosition)
    {
        if (_visualToBodyId.TryGetValue(visual, out var bodyId))
        {
            var body = PhysicsEngine.GetBodyById(bodyId);
            if (body is not null)
            {
                body.Position = newPosition;
                body.Velocity = new Vector3D(0, 0, 0);
                body.IsAtRest = false;
                PhysicsEngine.WakeAllBodies();
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _updateCoordinator.BatchedUpdateReady -= OnBatchedPhysicsUpdate;
        _updateCoordinator.Dispose();
        
        _themeService.Dispose();
        
        PhysicsEngine.BodiesUpdated -= OnPhysicsBodiesUpdated;
        PhysicsEngine.Stop();
        PhysicsEngine.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}
