using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Infrastructure;
using _3DObjectViewer.Core.Models;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// ViewModel for managing 3D object creation and settings.
/// </summary>
public class ObjectsViewModel : ViewModelBase
{
    private readonly Random _random = new();
    private double _objectSize = 1.0;
    private ObjectColor _selectedColor = ObjectColor.Red;
    private double _dropHeight = 5.0;
    private int _dropCount = 1;
    
    // Cached frozen materials for common colors to avoid recreating brushes
    private static readonly Dictionary<ObjectColor, Material> FrozenMaterials = CreateFrozenMaterials();

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectsViewModel"/> class.
    /// </summary>
    /// <param name="sceneObjects">The shared collection of scene objects.</param>
    public ObjectsViewModel(ObservableCollection<Visual3D> sceneObjects)
    {
        SceneObjects = sceneObjects;

        AddCubeCommand = new RelayCommand(AddCubes);
        AddSphereCommand = new RelayCommand(AddSpheres);
        AddCylinderCommand = new RelayCommand(AddCylinders);
        AddConeCommand = new RelayCommand(AddCones);
        AddTorusCommand = new RelayCommand(AddToruses);
    }
    
    private static Dictionary<ObjectColor, Material> CreateFrozenMaterials()
    {
        var materials = new Dictionary<ObjectColor, Material>
        {
            [ObjectColor.Red] = CreateShinyMaterial(Colors.Red),
            [ObjectColor.Green] = CreateShinyMaterial(Colors.Green),
            [ObjectColor.Blue] = CreateShinyMaterial(Colors.Blue),
            [ObjectColor.Yellow] = CreateShinyMaterial(Colors.Yellow)
        };
        
        return materials;
    }

    /// <summary>
    /// Creates a shiny material with both diffuse and specular components.
    /// </summary>
    private static Material CreateShinyMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        
        // Add diffuse component for base color
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        
        // Add specular highlight for shininess
        materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 40));
        
        // Add subtle emissive component to ensure visibility on flat surfaces
        var emissiveColor = Color.FromArgb(30, color.R, color.G, color.B);
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
        
        materialGroup.Freeze();
        return materialGroup;
    }

    /// <summary>
    /// Occurs when a new object is added and needs physics.
    /// </summary>
    public event Action<Visual3D, Point3D>? ObjectAdded;

    /// <summary>
    /// Gets the collection of 3D objects in the scene.
    /// </summary>
    public ObservableCollection<Visual3D> SceneObjects { get; }

    /// <summary>
    /// Gets or sets the size of newly created objects.
    /// </summary>
    public double ObjectSize
    {
        get => _objectSize;
        set => SetProperty(ref _objectSize, value);
    }

    /// <summary>
    /// Gets or sets the color for newly created objects.
    /// </summary>
    public ObjectColor SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    /// <summary>
    /// Gets or sets the drop height for new objects.
    /// </summary>
    public double DropHeight
    {
        get => _dropHeight;
        set => SetProperty(ref _dropHeight, value);
    }

    /// <summary>
    /// Gets or sets the number of objects to drop at once.
    /// </summary>
    public int DropCount
    {
        get => _dropCount;
        set => SetProperty(ref _dropCount, Math.Clamp(value, 1, 50));
    }

    /// <summary>
    /// Gets the command to add cubes to the scene.
    /// </summary>
    public ICommand AddCubeCommand { get; }

    /// <summary>
    /// Gets the command to add spheres to the scene.
    /// </summary>
    public ICommand AddSphereCommand { get; }

    /// <summary>
    /// Gets the command to add cylinders to the scene.
    /// </summary>
    public ICommand AddCylinderCommand { get; }

    /// <summary>
    /// Gets the command to add cones to the scene.
    /// </summary>
    public ICommand AddConeCommand { get; }

    /// <summary>
    /// Gets the command to add toruses to the scene.
    /// </summary>
    public ICommand AddTorusCommand { get; }

    #region Batch Add Methods

    private void AddCubes()
    {
        for (int i = 0; i < DropCount; i++)
        {
            AddCube();
        }
    }

    private void AddSpheres()
    {
        for (int i = 0; i < DropCount; i++)
        {
            AddSphere();
        }
    }

    private void AddCylinders()
    {
        for (int i = 0; i < DropCount; i++)
        {
            AddCylinder();
        }
    }

    private void AddCones()
    {
        for (int i = 0; i < DropCount; i++)
        {
            AddCone();
        }
    }

    private void AddToruses()
    {
        for (int i = 0; i < DropCount; i++)
        {
            AddTorus();
        }
    }

    #endregion

    #region Single Object Creation

    private void AddCube()
    {
        var position = GetDropPosition(ObjectSize);
        var cube = new BoxVisual3D
        {
            Width = ObjectSize,
            Height = ObjectSize,
            Length = ObjectSize,
            Center = position,
            Material = CreateMaterial()
        };
        SceneObjects.Add(cube);
        ObjectAdded?.Invoke(cube, position);
    }

    private void AddSphere()
    {
        var position = GetDropPosition(ObjectSize);
        var sphere = new SphereVisual3D
        {
            Radius = ObjectSize / 2,
            Center = position,
            Material = CreateMaterial()
        };
        SceneObjects.Add(sphere);
        ObjectAdded?.Invoke(sphere, position);
    }

    private void AddCylinder()
    {
        var position = GetDropPosition(ObjectSize * 2);
        var cylinder = new PipeVisual3D
        {
            Diameter = ObjectSize,
            InnerDiameter = 0,
            Point1 = new Point3D(position.X, position.Y, position.Z - ObjectSize),
            Point2 = new Point3D(position.X, position.Y, position.Z + ObjectSize),
            Material = CreateMaterial()
        };
        SceneObjects.Add(cylinder);
        ObjectAdded?.Invoke(cylinder, position);
    }

    private void AddCone()
    {
        var position = GetDropPosition(ObjectSize * 2);
        var cone = new TruncatedConeVisual3D
        {
            BaseRadius = ObjectSize / 2,
            TopRadius = 0,
            Height = ObjectSize * 2,
            Origin = new Point3D(position.X, position.Y, position.Z - ObjectSize),
            Material = CreateMaterial()
        };
        SceneObjects.Add(cone);
        ObjectAdded?.Invoke(cone, position);
    }

    private void AddTorus()
    {
        var position = GetDropPosition(ObjectSize / 3);
        var torus = new TorusVisual3D
        {
            TorusDiameter = ObjectSize * 2,
            TubeDiameter = ObjectSize / 3,
            Material = CreateMaterial()
        };
        torus.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);
        SceneObjects.Add(torus);
        ObjectAdded?.Invoke(torus, position);
    }

    #endregion

    /// <summary>
    /// Creates a material with the currently selected color.
    /// </summary>
    public Material CreateMaterial()
    {
        // Use cached frozen material for standard colors (better GPU performance)
        if (SelectedColor != ObjectColor.Random && FrozenMaterials.TryGetValue(SelectedColor, out var cachedMaterial))
        {
            return cachedMaterial;
        }
        
        // Random color needs a new material each time
        return CreateShinyMaterial(GetColorValue());
    }

    private Color GetColorValue()
    {
        return SelectedColor switch
        {
            ObjectColor.Red => Colors.Red,
            ObjectColor.Green => Colors.Green,
            ObjectColor.Blue => Colors.Blue,
            ObjectColor.Yellow => Colors.Yellow,
            ObjectColor.Random => Color.FromRgb(
                (byte)_random.Next(256),
                (byte)_random.Next(256),
                (byte)_random.Next(256)),
            _ => Colors.Gray
        };
    }

    /// <summary>
    /// Gets a random position at drop height for physics simulation.
    /// </summary>
    private Point3D GetDropPosition(double objectHeight)
    {
        // Spread objects more when dropping multiple (extended by factor 4)
        double spreadFactor = DropCount > 1 ? 32 : 24;
        double halfSpread = spreadFactor / 2;
        
        return new Point3D(
            _random.NextDouble() * spreadFactor - halfSpread,
            _random.NextDouble() * spreadFactor - halfSpread,
            DropHeight + objectHeight / 2 + _random.NextDouble() * 2  // Slight height variation
        );
    }
}
