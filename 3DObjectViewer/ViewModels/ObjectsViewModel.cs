using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Infrastructure;
using _3DObjectViewer.Core.Models;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// Defines the types of 3D objects that can be created.
/// </summary>
public enum ObjectType
{
    Cube,
    Sphere,
    Cylinder,
    Cone,
    Torus
}

/// <summary>
/// ViewModel for managing 3D object creation and settings.
/// </summary>
public class ObjectsViewModel : ViewModelBase
{
    private readonly Random _random = new();
    private double _objectSize = 1.0;
    private ObjectColor _selectedColor = ObjectColor.Random;
    private MaterialStyle _selectedMaterialStyle = MaterialStyle.Random;
    private double _dropHeight = 15.0;
    private int _dropCount = 20;
    
    // Cached frozen materials for common color/material combinations
    private static readonly Dictionary<(ObjectColor, MaterialStyle), Material> CachedMaterials = new();
    private static readonly object CacheLock = new();
    
    // All available object types for random selection
    private static readonly ObjectType[] AllObjectTypes = 
        [ObjectType.Cube, ObjectType.Sphere, ObjectType.Cylinder, ObjectType.Cone, ObjectType.Torus];
    
    // All non-random material styles for random selection
    private static readonly MaterialStyle[] NonRandomMaterialStyles =
        [MaterialStyle.Shiny, MaterialStyle.Metallic, MaterialStyle.Matte, 
         MaterialStyle.Glass, MaterialStyle.Glowing, MaterialStyle.Neon];

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
        AddRandomCommand = new RelayCommand(AddRandomObjects);
    }

    #region Material Creation

    /// <summary>
    /// Creates a material based on the specified color and material style.
    /// </summary>
    private static Material CreateMaterialForStyle(Color color, MaterialStyle materialStyle)
    {
        return materialStyle switch
        {
            MaterialStyle.Shiny => CreateShinyMaterial(color),
            MaterialStyle.Metallic => CreateMetallicMaterial(color),
            MaterialStyle.Matte => CreateMatteMaterial(color),
            MaterialStyle.Glass => CreateGlassMaterial(color),
            MaterialStyle.Glowing => CreateGlowingMaterial(color),
            MaterialStyle.Neon => CreateNeonMaterial(color),
            _ => CreateShinyMaterial(color)
        };
    }

    /// <summary>
    /// Creates a shiny material with both diffuse and specular components.
    /// </summary>
    private static Material CreateShinyMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 40));
        var emissiveColor = Color.FromArgb(30, color.R, color.G, color.B);
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
        materialGroup.Freeze();
        return materialGroup;
    }

    /// <summary>
    /// Creates a highly reflective metallic material.
    /// </summary>
    private static Material CreateMetallicMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        var darkColor = Color.FromRgb(
            (byte)(color.R * 0.3), (byte)(color.G * 0.3), (byte)(color.B * 0.3));
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(darkColor)));
        var specColor = Color.FromRgb(
            (byte)Math.Min(255, color.R + 50),
            (byte)Math.Min(255, color.G + 50),
            (byte)Math.Min(255, color.B + 50));
        materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(specColor), 100));
        var emissiveColor = Color.FromArgb(40, color.R, color.G, color.B);
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
        materialGroup.Freeze();
        return materialGroup;
    }

    /// <summary>
    /// Creates a matte material with no specular reflection.
    /// </summary>
    private static Material CreateMatteMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        var emissiveColor = Color.FromArgb(15, color.R, color.G, color.B);
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
        materialGroup.Freeze();
        return materialGroup;
    }

    /// <summary>
    /// Creates a semi-transparent glass-like material.
    /// </summary>
    private static Material CreateGlassMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        var transparentColor = Color.FromArgb(120, color.R, color.G, color.B);
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(transparentColor)));
        materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 120));
        var emissiveColor = Color.FromArgb(50, color.R, color.G, color.B);
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
        materialGroup.Freeze();
        return materialGroup;
    }

    /// <summary>
    /// Creates an emissive material that appears to glow.
    /// </summary>
    private static Material CreateGlowingMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        var dimColor = Color.FromRgb(
            (byte)(color.R * 0.4), (byte)(color.G * 0.4), (byte)(color.B * 0.4));
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(dimColor)));
        var glowColor = Color.FromArgb(200, color.R, color.G, color.B);
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(glowColor)));
        materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Colors.White), 20));
        materialGroup.Freeze();
        return materialGroup;
    }

    /// <summary>
    /// Creates a bright neon material with strong emission.
    /// </summary>
    private static Material CreateNeonMaterial(Color color)
    {
        var materialGroup = new MaterialGroup();
        var brightColor = Color.FromRgb(
            (byte)Math.Min(255, color.R + 30),
            (byte)Math.Min(255, color.G + 30),
            (byte)Math.Min(255, color.B + 30));
        var darkColor = Color.FromRgb(
            (byte)(color.R * 0.2), (byte)(color.G * 0.2), (byte)(color.B * 0.2));
        materialGroup.Children.Add(new DiffuseMaterial(new SolidColorBrush(darkColor)));
        materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(brightColor)));
        materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(brightColor), 60));
        materialGroup.Freeze();
        return materialGroup;
    }

    #endregion

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
    /// Gets or sets the material type for newly created objects.
    /// </summary>
    public MaterialStyle SelectedMaterialStyle
    {
        get => _selectedMaterialStyle;
        set => SetProperty(ref _selectedMaterialStyle, value);
    }

    /// <summary>
    /// Gets or sets the drop height for new objects (2-100 meters).
    /// </summary>
    public double DropHeight
    {
        get => _dropHeight;
        set => SetProperty(ref _dropHeight, Math.Clamp(value, 2, 100));
    }

    /// <summary>
    /// Gets or sets the number of objects to drop at once (1-250).
    /// </summary>
    public int DropCount
    {
        get => _dropCount;
        set => SetProperty(ref _dropCount, Math.Clamp(value, 1, 250));
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

    /// <summary>
    /// Gets the command to add random objects to the scene.
    /// </summary>
    public ICommand AddRandomCommand { get; }

    #region Batch Add Methods

    private void AddCubes()
    {
        for (int i = 0; i < DropCount; i++) AddCube();
    }

    private void AddSpheres()
    {
        for (int i = 0; i < DropCount; i++) AddSphere();
    }

    private void AddCylinders()
    {
        for (int i = 0; i < DropCount; i++) AddCylinder();
    }

    private void AddCones()
    {
        for (int i = 0; i < DropCount; i++) AddCone();
    }

    private void AddToruses()
    {
        for (int i = 0; i < DropCount; i++) AddTorus();
    }

    private void AddRandomObjects()
    {
        for (int i = 0; i < DropCount; i++) AddRandomObject();
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

    private void AddRandomObject()
    {
        var randomObjectType = AllObjectTypes[_random.Next(AllObjectTypes.Length)];
        
        switch (randomObjectType)
        {
            case ObjectType.Cube: AddCube(); break;
            case ObjectType.Sphere: AddSphere(); break;
            case ObjectType.Cylinder: AddCylinder(); break;
            case ObjectType.Cone: AddCone(); break;
            case ObjectType.Torus: AddTorus(); break;
        }
    }

    #endregion

    /// <summary>
    /// Creates a material with the currently selected color and material style.
    /// </summary>
    public Material CreateMaterial()
    {
        var color = GetColorValue();
        var materialStyle = GetMaterialStyle();
        
        // Random combinations always get fresh materials
        if (SelectedColor == ObjectColor.Random || SelectedMaterialStyle == MaterialStyle.Random)
        {
            return CreateMaterialForStyle(color, materialStyle);
        }
        
        // Use cached materials for standard color/material combinations
        var key = (SelectedColor, materialStyle);
        lock (CacheLock)
        {
            if (CachedMaterials.TryGetValue(key, out var cachedMaterial))
            {
                return cachedMaterial;
            }
            
            var material = CreateMaterialForStyle(color, materialStyle);
            CachedMaterials[key] = material;
            return material;
        }
    }

    private MaterialStyle GetMaterialStyle()
    {
        if (SelectedMaterialStyle == MaterialStyle.Random)
        {
            return NonRandomMaterialStyles[_random.Next(NonRandomMaterialStyles.Length)];
        }
        return SelectedMaterialStyle;
    }

    private Color GetColorValue()
    {
        return SelectedColor switch
        {
            ObjectColor.Red => Colors.Red,
            ObjectColor.Green => Colors.Green,
            ObjectColor.Blue => Colors.Blue,
            ObjectColor.Yellow => Colors.Yellow,
            ObjectColor.Orange => Colors.Orange,
            ObjectColor.Purple => Colors.Purple,
            ObjectColor.Cyan => Colors.Cyan,
            ObjectColor.Pink => Colors.HotPink,
            ObjectColor.White => Colors.White,
            ObjectColor.Gold => Colors.Gold,
            ObjectColor.Random => Color.FromRgb(
                (byte)_random.Next(100, 256),
                (byte)_random.Next(100, 256),
                (byte)_random.Next(100, 256)),
            _ => Colors.Gray
        };
    }

    /// <summary>
    /// Gets a random position at drop height for physics simulation.
    /// </summary>
    private Point3D GetDropPosition(double objectHeight)
    {
        double spreadFactor = DropCount > 1 ? 32 : 24;
        double halfSpread = spreadFactor / 2;
        
        return new Point3D(
            _random.NextDouble() * spreadFactor - halfSpread,
            _random.NextDouble() * spreadFactor - halfSpread,
            DropHeight + objectHeight / 2 + _random.NextDouble() * 2
        );
    }
}
