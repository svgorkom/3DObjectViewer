using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Infrastructure;
using _3DObjectViewer.Core.Models;
using _3DObjectViewer.Core.Physics;
using _3DObjectViewer.Physics;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// Main ViewModel that orchestrates the 3D Object Viewer application.
/// </summary>
/// <remarks>
/// This ViewModel composes the specialized ViewModels for objects, selection, and lighting,
/// providing a unified interface for the main window.
/// </remarks>
public class MainViewModel : ViewModelBase
{
    private bool _physicsEnabled = true;
    private double _gravity = 9.81;
    private int _physicsUpdateThrottle;
    private PhysicsEngineType _physicsEngineType = PhysicsEngineType.BepuThreaded;
    
    // Mapping from Visual3D to physics body Guid for O(1) lookup
    private readonly Dictionary<Visual3D, Guid> _visualToBodyId = [];
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class
    /// using the default threaded physics engine.
    /// </summary>
    public MainViewModel() : this(PhysicsEngineType.BepuThreaded)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class
    /// with the specified physics engine type.
    /// </summary>
    /// <param name="physicsEngineType">The type of physics engine to use.</param>
    public MainViewModel(PhysicsEngineType physicsEngineType)
    {
        _physicsEngineType = physicsEngineType;
        
        SceneObjects = [];
        PhysicsEngine = new PhysicsEngine(physicsEngineType);

        // Create child ViewModels
        Objects = new ObjectsViewModel(SceneObjects);
        Selection = new SelectionViewModel(SceneObjects, Objects);
        Lighting = new LightingViewModel();
        PerformanceStats = new PerformanceStatsViewModel();

        // Scene commands
        ClearAllCommand = new RelayCommand(ClearAll, () => SceneObjects.Count > 0);
        ResetCameraCommand = new RelayCommand(() => ResetCameraRequested?.Invoke());
        TogglePhysicsCommand = new RelayCommand(TogglePhysics);
        DropAllCommand = new RelayCommand(DropAll, () => SceneObjects.Count > 0);

        // Forward events
        Selection.SelectionChanged += () => SelectionChanged?.Invoke();
        Selection.ObjectDeleted += OnObjectDeleted;
        Selection.ObjectMoved += OnObjectMoved;
        Lighting.LightingChanged += () => LightingChanged?.Invoke();

        // Subscribe to object additions for physics
        Objects.ObjectAdded += OnObjectAdded;

        // Configure physics engine - use batched updates for better performance
        PhysicsEngine.Gravity = Gravity;
        PhysicsEngine.BodiesUpdated += OnPhysicsBodiesUpdated;

        // Configure bucket bounds (matching grid size: 20x20, height: 4m)
        PhysicsEngine.SetBucketBounds(-10, 10, -10, 10, 4);

        // Start physics
        if (PhysicsEnabled)
        {
            PhysicsEngine.Start();
        }
    }

    #region Events

    /// <summary>
    /// Occurs when a camera reset is requested.
    /// </summary>
    public event Action? ResetCameraRequested;

    /// <summary>
    /// Occurs when the selection visual needs to be updated.
    /// </summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Occurs when lighting configuration changes.
    /// </summary>
    public event Action? LightingChanged;

    /// <summary>
    /// Occurs when physics updates an object's position.
    /// </summary>
    public event Action? PhysicsUpdated;

    #endregion

    #region Child ViewModels and Services

    /// <summary>
    /// Gets the ViewModel for object creation and settings.
    /// </summary>
    public ObjectsViewModel Objects { get; }

    /// <summary>
    /// Gets the ViewModel for selected object manipulation.
    /// </summary>
    public SelectionViewModel Selection { get; }

    /// <summary>
    /// Gets the ViewModel for lighting management.
    /// </summary>
    public LightingViewModel Lighting { get; }

    /// <summary>
    /// Gets the ViewModel for performance statistics display.
    /// </summary>
    public PerformanceStatsViewModel PerformanceStats { get; }

    /// <summary>
    /// Gets the physics engine.
    /// </summary>
    public PhysicsEngine PhysicsEngine { get; }

    #endregion

    #region Shared Collections

    /// <summary>
    /// Gets the collection of 3D objects currently in the scene.
    /// </summary>
    public ObservableCollection<Visual3D> SceneObjects { get; }

    #endregion

    #region Physics Properties

    /// <summary>
    /// Gets the current physics engine type.
    /// </summary>
    public PhysicsEngineType PhysicsEngineType => _physicsEngineType;

    /// <summary>
    /// Gets the name of the current physics engine.
    /// </summary>
    public string PhysicsEngineName => PhysicsEngine.Name;

    /// <summary>
    /// Gets or sets whether physics simulation is enabled.
    /// </summary>
    public bool PhysicsEnabled
    {
        get => _physicsEnabled;
        set
        {
            if (SetProperty(ref _physicsEnabled, value))
            {
                if (value)
                    PhysicsEngine.Start();
                else
                    PhysicsEngine.Stop();
            }
        }
    }

    /// <summary>
    /// Gets or sets the gravity strength.
    /// </summary>
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

    #endregion

    #region Convenience Properties (for backward compatibility in bindings)

    /// <summary>
    /// Gets a value indicating whether an object is currently selected.
    /// </summary>
    public bool HasSelection => Selection.HasSelection;

    /// <summary>
    /// Gets the collection of light sources.
    /// </summary>
    public ObservableCollection<LightSource> LightSources => Lighting.LightSources;

    /// <summary>
    /// Gets or sets the currently selected light source.
    /// </summary>
    public LightSource? SelectedLight
    {
        get => Lighting.SelectedLight;
        set => Lighting.SelectedLight = value;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Gets the command to clear all objects from the scene.
    /// </summary>
    public ICommand ClearAllCommand { get; }

    /// <summary>
    /// Gets the command to reset the camera to its default position.
    /// </summary>
    public ICommand ResetCameraCommand { get; }

    /// <summary>
    /// Gets the command to toggle physics simulation.
    /// </summary>
    public ICommand TogglePhysicsCommand { get; }

    /// <summary>
    /// Gets the command to drop all objects.
    /// </summary>
    public ICommand DropAllCommand { get; }

    #endregion

    #region Methods

    private void ClearAll()
    {
        Selection.SelectedObject = null;
        PhysicsEngine.Clear();
        PhysicsHelper.ClearVisualCache();
        _visualToBodyId.Clear();
        SceneObjects.Clear();
    }

    private void TogglePhysics()
    {
        PhysicsEnabled = !PhysicsEnabled;
    }

    private void DropAll()
    {
        PhysicsEngine.WakeAllBodies();
    }

    private void OnObjectAdded(Visual3D visual, Point3D position)
    {
        var body = PhysicsHelper.CreateRigidBody(visual, position);
        
        // Add a small random horizontal velocity to make physics more dynamic
        body.Velocity = new Vector3D(
            (_random.NextDouble() - 0.5) * 2.0,  // Random X velocity: -1 to +1
            (_random.NextDouble() - 0.5) * 2.0,  // Random Y velocity: -1 to +1
            0);
        
        PhysicsEngine.AddBody(body);
        _visualToBodyId[visual] = body.Id;
    }

    private void OnPhysicsBodiesUpdated(IReadOnlyList<RigidBody> bodies)
    {
        // Apply transforms in batch - more efficient than individual events
        int count = bodies.Count;
        for (int i = 0; i < count; i++)
        {
            PhysicsHelper.ApplyTransformToVisual(bodies[i]);
        }
        
        // Throttle the PhysicsUpdated event to reduce update frequency
        _physicsUpdateThrottle++;
        if (_physicsUpdateThrottle >= 2) // Fire every other frame
        {
            _physicsUpdateThrottle = 0;
            PhysicsUpdated?.Invoke();
        }
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
                PhysicsEngine.WakeAllBodies(); // Wake the body in case it was sleeping
            }
        }
    }

    #endregion
}
