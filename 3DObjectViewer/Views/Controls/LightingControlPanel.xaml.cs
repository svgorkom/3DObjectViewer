using System.Windows;
using System.Windows.Controls;
using _3DObjectViewer.ViewModels;

namespace _3DObjectViewer.Views.Controls;

/// <summary>
/// Control panel for managing lighting in the 3D scene.
/// </summary>
public partial class LightingControlPanel : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LightingControlPanel"/> class.
    /// </summary>
    public LightingControlPanel()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void PresetDaylight_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Lighting.ApplyDaylightPreset();
    }

    private void PresetSunset_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Lighting.ApplySunsetPreset();
    }

    private void PresetMoonlight_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Lighting.ApplyMoonlightPreset();
    }

    private void PresetStudio_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.Lighting.ApplyStudioPreset();
    }
}
