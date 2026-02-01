using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using _3DObjectViewer.Core.Infrastructure;
using HelixToolkit.Wpf;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// ViewModel for managing selected object properties and transformations.
/// </summary>
public class SelectionViewModel : ViewModelBase
{
    private readonly ObservableCollection<Visual3D> _sceneObjects;
    private readonly ObjectsViewModel _objectsViewModel;
    
    private Visual3D? _selectedObject;
    private bool _hasSelection;
    private double _selectedObjectScale = 1.0;
    private double _selectedObjectRotationX;
    private double _selectedObjectRotationY;
    private double _selectedObjectRotationZ;
    private double _selectedObjectPositionX;
    private double _selectedObjectPositionY;
    private double _selectedObjectPositionZ;
    private bool _isUpdatingPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectionViewModel"/> class.
    /// </summary>
    /// <param name="sceneObjects">The shared collection of scene objects.</param>
    /// <param name="objectsViewModel">The objects view model for material creation.</param>
    public SelectionViewModel(ObservableCollection<Visual3D> sceneObjects, ObjectsViewModel objectsViewModel)
    {
        _sceneObjects = sceneObjects;
        _objectsViewModel = objectsViewModel;

        DeselectCommand = new RelayCommand(Deselect, () => HasSelection);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => HasSelection);
        ApplyColorToSelectedCommand = new RelayCommand(ApplyColorToSelected, () => HasSelection);
    }

    /// <summary>
    /// Occurs when the selection visual needs to be updated.
    /// </summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Occurs when an object is deleted.
    /// </summary>
    public event Action<Visual3D>? ObjectDeleted;

    /// <summary>
    /// Occurs when an object's position is changed manually.
    /// </summary>
    public event Action<Visual3D, Point3D>? ObjectMoved;

    /// <summary>
    /// Gets or sets the currently selected 3D object.
    /// </summary>
    public Visual3D? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value))
            {
                HasSelection = value is not null;
                if (value is not null)
                {
                    LoadSelectedObjectProperties();
                }
                SelectionChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether an object is currently selected.
    /// </summary>
    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    /// <summary>
    /// Gets or sets the X position of the selected object.
    /// </summary>
    public double SelectedObjectPositionX
    {
        get => _selectedObjectPositionX;
        set
        {
            if (SetProperty(ref _selectedObjectPositionX, value) && !_isUpdatingPosition)
            {
                ApplyPositionToSelected();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Y position of the selected object.
    /// </summary>
    public double SelectedObjectPositionY
    {
        get => _selectedObjectPositionY;
        set
        {
            if (SetProperty(ref _selectedObjectPositionY, value) && !_isUpdatingPosition)
            {
                ApplyPositionToSelected();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Z position of the selected object.
    /// </summary>
    public double SelectedObjectPositionZ
    {
        get => _selectedObjectPositionZ;
        set
        {
            if (SetProperty(ref _selectedObjectPositionZ, value) && !_isUpdatingPosition)
            {
                ApplyPositionToSelected();
            }
        }
    }

    /// <summary>
    /// Gets or sets the scale factor of the selected object.
    /// </summary>
    public double SelectedObjectScale
    {
        get => _selectedObjectScale;
        set
        {
            if (SetProperty(ref _selectedObjectScale, value))
            {
                ApplyTransformToSelected();
            }
        }
    }

    /// <summary>
    /// Gets or sets the X-axis rotation of the selected object in degrees.
    /// </summary>
    public double SelectedObjectRotationX
    {
        get => _selectedObjectRotationX;
        set
        {
            if (SetProperty(ref _selectedObjectRotationX, value))
            {
                ApplyTransformToSelected();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Y-axis rotation of the selected object in degrees.
    /// </summary>
    public double SelectedObjectRotationY
    {
        get => _selectedObjectRotationY;
        set
        {
            if (SetProperty(ref _selectedObjectRotationY, value))
            {
                ApplyTransformToSelected();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Z-axis rotation of the selected object in degrees.
    /// </summary>
    public double SelectedObjectRotationZ
    {
        get => _selectedObjectRotationZ;
        set
        {
            if (SetProperty(ref _selectedObjectRotationZ, value))
            {
                ApplyTransformToSelected();
            }
        }
    }

    /// <summary>
    /// Gets the command to deselect the currently selected object.
    /// </summary>
    public ICommand DeselectCommand { get; }

    /// <summary>
    /// Gets the command to delete the currently selected object.
    /// </summary>
    public ICommand DeleteSelectedCommand { get; }

    /// <summary>
    /// Gets the command to apply the current color to the selected object.
    /// </summary>
    public ICommand ApplyColorToSelectedCommand { get; }

    /// <summary>
    /// Moves the selected object to a new position.
    /// </summary>
    public void MoveSelectedObject(Point3D newPosition)
    {
        if (SelectedObject is null)
            return;

        SetObjectPosition(SelectedObject, newPosition);

        _isUpdatingPosition = true;
        SelectedObjectPositionX = newPosition.X;
        SelectedObjectPositionY = newPosition.Y;
        SelectedObjectPositionZ = newPosition.Z;
        _isUpdatingPosition = false;

        ObjectMoved?.Invoke(SelectedObject, newPosition);
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Gets the current position of the selected object.
    /// </summary>
    public Point3D GetSelectedObjectPosition()
    {
        if (SelectedObject is null)
            return new Point3D(0, 0, 0);

        return GetObjectPosition(SelectedObject);
    }

    private void ApplyPositionToSelected()
    {
        if (SelectedObject is null)
            return;

        var newPosition = new Point3D(SelectedObjectPositionX, SelectedObjectPositionY, SelectedObjectPositionZ);
        SetObjectPosition(SelectedObject, newPosition);
        ObjectMoved?.Invoke(SelectedObject, newPosition);
        SelectionChanged?.Invoke();
    }

    private void LoadSelectedObjectProperties()
    {
        if (SelectedObject is null)
            return;

        var position = GetObjectPosition(SelectedObject);

        _isUpdatingPosition = true;
        _selectedObjectPositionX = position.X;
        _selectedObjectPositionY = position.Y;
        _selectedObjectPositionZ = position.Z;
        _isUpdatingPosition = false;

        _selectedObjectScale = 1.0;
        _selectedObjectRotationX = 0;
        _selectedObjectRotationY = 0;
        _selectedObjectRotationZ = 0;

        OnPropertyChanged(nameof(SelectedObjectPositionX));
        OnPropertyChanged(nameof(SelectedObjectPositionY));
        OnPropertyChanged(nameof(SelectedObjectPositionZ));
        OnPropertyChanged(nameof(SelectedObjectScale));
        OnPropertyChanged(nameof(SelectedObjectRotationX));
        OnPropertyChanged(nameof(SelectedObjectRotationY));
        OnPropertyChanged(nameof(SelectedObjectRotationZ));
    }

    private void ApplyTransformToSelected()
    {
        if (SelectedObject is null)
            return;

        var position = new Point3D(SelectedObjectPositionX, SelectedObjectPositionY, SelectedObjectPositionZ);

        // Build combined matrix directly for better performance
        // Order: Scale -> RotateX -> RotateY -> RotateZ -> Translate
        var matrix = Matrix3D.Identity;
        matrix.Scale(new Vector3D(SelectedObjectScale, SelectedObjectScale, SelectedObjectScale));
        matrix.Rotate(new Quaternion(new Vector3D(1, 0, 0), SelectedObjectRotationX));
        matrix.Rotate(new Quaternion(new Vector3D(0, 1, 0), SelectedObjectRotationY));
        matrix.Rotate(new Quaternion(new Vector3D(0, 0, 1), SelectedObjectRotationZ));
        matrix.Translate(new Vector3D(position.X, position.Y, position.Z));

        SelectedObject.Transform = new MatrixTransform3D(matrix);
        SelectionChanged?.Invoke();
    }

    private static Point3D GetObjectPosition(Visual3D obj)
    {
        return obj switch
        {
            BoxVisual3D box => box.Center,
            SphereVisual3D sphere => sphere.Center,
            PipeVisual3D pipe => pipe.Point1,
            TruncatedConeVisual3D cone => cone.Origin,
            TorusVisual3D torus => GetTransformPosition(torus.Transform),
            _ => GetTransformPosition(obj.Transform)
        };
    }

    private static Point3D GetTransformPosition(Transform3D? transform)
    {
        if (transform is null)
            return new Point3D(0, 0, 0);

        if (transform is TranslateTransform3D translate)
            return new Point3D(translate.OffsetX, translate.OffsetY, translate.OffsetZ);

        if (transform is MatrixTransform3D matrixTransform)
        {
            var matrix = matrixTransform.Matrix;
            return new Point3D(matrix.OffsetX, matrix.OffsetY, matrix.OffsetZ);
        }

        if (transform is Transform3DGroup group)
        {
            foreach (var t in group.Children)
            {
                if (t is TranslateTransform3D tt)
                    return new Point3D(tt.OffsetX, tt.OffsetY, tt.OffsetZ);
            }
        }

        return new Point3D(0, 0, 0);
    }

    private void SetObjectPosition(Visual3D obj, Point3D position)
    {
        switch (obj)
        {
            case BoxVisual3D box:
                box.Center = position;
                break;
            case SphereVisual3D sphere:
                sphere.Center = position;
                break;
            case PipeVisual3D pipe:
                var delta = new Vector3D(
                    position.X - pipe.Point1.X,
                    position.Y - pipe.Point1.Y,
                    position.Z - pipe.Point1.Z);
                pipe.Point1 = position;
                pipe.Point2 = new Point3D(
                    pipe.Point2.X + delta.X,
                    pipe.Point2.Y + delta.Y,
                    pipe.Point2.Z + delta.Z);
                break;
            case TruncatedConeVisual3D cone:
                cone.Origin = position;
                break;
            default:
                UpdateTransformPosition(obj, position);
                break;
        }
    }

    private void UpdateTransformPosition(Visual3D obj, Point3D position)
    {
        if (obj.Transform is MatrixTransform3D existingMatrix)
        {
            // Update position in existing matrix by replacing the translation component
            var matrix = existingMatrix.Matrix;
            matrix.OffsetX = position.X;
            matrix.OffsetY = position.Y;
            matrix.OffsetZ = position.Z;
            obj.Transform = new MatrixTransform3D(matrix);
        }
        else if (obj.Transform is Transform3DGroup group)
        {
            for (int i = 0; i < group.Children.Count; i++)
            {
                if (group.Children[i] is TranslateTransform3D)
                {
                    group.Children[i] = new TranslateTransform3D(position.X, position.Y, position.Z);
                    return;
                }
            }
            group.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));
        }
        else
        {
            // Build combined matrix directly for better performance
            var matrix = Matrix3D.Identity;
            matrix.Scale(new Vector3D(SelectedObjectScale, SelectedObjectScale, SelectedObjectScale));
            matrix.Rotate(new Quaternion(new Vector3D(1, 0, 0), SelectedObjectRotationX));
            matrix.Rotate(new Quaternion(new Vector3D(0, 1, 0), SelectedObjectRotationY));
            matrix.Rotate(new Quaternion(new Vector3D(0, 0, 1), SelectedObjectRotationZ));
            matrix.Translate(new Vector3D(position.X, position.Y, position.Z));
            obj.Transform = new MatrixTransform3D(matrix);
        }
    }

    private void Deselect()
    {
        SelectedObject = null;
    }

    private void DeleteSelected()
    {
        if (SelectedObject is not null)
        {
            var objToDelete = SelectedObject;
            _sceneObjects.Remove(objToDelete);
            ObjectDeleted?.Invoke(objToDelete);
            SelectedObject = null;
        }
    }

    private void ApplyColorToSelected()
    {
        if (SelectedObject is MeshElement3D meshElement)
        {
            meshElement.Material = _objectsViewModel.CreateMaterial();
        }
    }
}
