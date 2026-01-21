using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using _3DObjectViewer.Core.Infrastructure;
using _3DObjectViewer.Core.Models;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// ViewModel for managing light sources in the 3D scene.
/// </summary>
public class LightingViewModel : ViewModelBase
{
    private readonly Random _random = new();
    private LightSource? _selectedLight;

    /// <summary>
    /// Initializes a new instance of the <see cref="LightingViewModel"/> class.
    /// </summary>
    public LightingViewModel()
    {
        LightSources = [];

        // Add default light
        var defaultLight = new LightSource("Main Light", -1, -1, -1, Colors.White, 1.0);
        LightSources.Add(defaultLight);
        SelectedLight = defaultLight;

        AddLightCommand = new RelayCommand(AddLight);
        RemoveLightCommand = new RelayCommand(RemoveSelectedLight, () => SelectedLight is not null && LightSources.Count > 1);
    }

    /// <summary>
    /// Occurs when lighting configuration changes.
    /// </summary>
    public event Action? LightingChanged;

    /// <summary>
    /// Gets the collection of light sources in the scene.
    /// </summary>
    public ObservableCollection<LightSource> LightSources { get; }

    /// <summary>
    /// Gets or sets the currently selected light source.
    /// </summary>
    public LightSource? SelectedLight
    {
        get => _selectedLight;
        set
        {
            if (_selectedLight is not null)
            {
                _selectedLight.PropertyChanged -= OnSelectedLightPropertyChanged;
            }

            if (SetProperty(ref _selectedLight, value))
            {
                if (_selectedLight is not null)
                {
                    _selectedLight.PropertyChanged += OnSelectedLightPropertyChanged;
                }
            }
        }
    }

    /// <summary>
    /// Gets the command to add a new light source.
    /// </summary>
    public ICommand AddLightCommand { get; }

    /// <summary>
    /// Gets the command to remove the selected light source.
    /// </summary>
    public ICommand RemoveLightCommand { get; }

    /// <summary>
    /// Applies the daylight preset to the selected light.
    /// </summary>
    public void ApplyDaylightPreset()
    {
        if (SelectedLight is null) return;

        SelectedLight.DirectionX = -0.5;
        SelectedLight.DirectionY = -0.5;
        SelectedLight.DirectionZ = -1;
        SelectedLight.PositionX = 5;
        SelectedLight.PositionY = 5;
        SelectedLight.PositionZ = 10;
        SelectedLight.Color = Colors.White;
        SelectedLight.Brightness = 1.0;
    }

    /// <summary>
    /// Applies the sunset preset to the selected light.
    /// </summary>
    public void ApplySunsetPreset()
    {
        if (SelectedLight is null) return;

        SelectedLight.DirectionX = -1;
        SelectedLight.DirectionY = -0.3;
        SelectedLight.DirectionZ = -0.3;
        SelectedLight.PositionX = 10;
        SelectedLight.PositionY = 3;
        SelectedLight.PositionZ = 3;
        SelectedLight.Color = Color.FromRgb(255, 160, 80);
        SelectedLight.Brightness = 0.9;
    }

    /// <summary>
    /// Applies the moonlight preset to the selected light.
    /// </summary>
    public void ApplyMoonlightPreset()
    {
        if (SelectedLight is null) return;

        SelectedLight.DirectionX = 0.3;
        SelectedLight.DirectionY = -0.5;
        SelectedLight.DirectionZ = -1;
        SelectedLight.PositionX = -3;
        SelectedLight.PositionY = 5;
        SelectedLight.PositionZ = 10;
        SelectedLight.Color = Color.FromRgb(180, 200, 255);
        SelectedLight.Brightness = 0.4;
    }

    /// <summary>
    /// Applies the studio preset with multiple lights.
    /// </summary>
    public void ApplyStudioPreset()
    {
        LightSources.Clear();

        var keyLight = new LightSource("Key Light", -1, -0.5, -1, Colors.White, 1.0, 8, 4, 8);
        LightSources.Add(keyLight);

        var fillLight = new LightSource("Fill Light", 0.8, -0.3, -0.5, Color.FromRgb(200, 200, 255), 0.5, -6, 2, 6);
        LightSources.Add(fillLight);

        var backLight = new LightSource("Back Light", 0, 1, -0.5, Color.FromRgb(255, 240, 200), 0.6, 0, -8, 7);
        LightSources.Add(backLight);

        SelectedLight = keyLight;
    }

    private void OnSelectedLightPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        LightingChanged?.Invoke();
    }

    private void AddLight()
    {
        var lightNumber = LightSources.Count + 1;
        var newLight = new LightSource(
            $"Light {lightNumber}",
            _random.NextDouble() * 2 - 1,
            _random.NextDouble() * 2 - 1,
            -1,
            Color.FromRgb(
                (byte)_random.Next(200, 256),
                (byte)_random.Next(200, 256),
                (byte)_random.Next(200, 256)),
            0.8);

        LightSources.Add(newLight);
        SelectedLight = newLight;
        LightingChanged?.Invoke();
    }

    private void RemoveSelectedLight()
    {
        if (SelectedLight is not null && LightSources.Count > 1)
        {
            var lightToRemove = SelectedLight;
            var index = LightSources.IndexOf(lightToRemove);
            LightSources.Remove(lightToRemove);

            SelectedLight = LightSources[Math.Max(0, index - 1)];
            LightingChanged?.Invoke();
        }
    }
}
