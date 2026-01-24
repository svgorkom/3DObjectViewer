using _3DObjectViewer.Core.Infrastructure;
using _3DObjectViewer.Core.Models;
using _3DObjectViewer.Services;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// ViewModel for application settings including theme configuration.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private AppTheme _themeMode = AppTheme.System;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    public SettingsViewModel(ThemeService themeService)
    {
        _themeService = themeService;
        _themeMode = themeService.CurrentMode;
    }

    /// <summary>
    /// Gets or sets the current theme mode.
    /// </summary>
    public AppTheme ThemeMode
    {
        get => _themeMode;
        set
        {
            if (SetProperty(ref _themeMode, value))
            {
                _themeService.CurrentMode = value;
            }
        }
    }

    /// <summary>
    /// Gets whether system theme is selected.
    /// </summary>
    public bool IsSystemTheme
    {
        get => _themeMode == AppTheme.System;
        set { if (value) ThemeMode = AppTheme.System; }
    }

    /// <summary>
    /// Gets whether light theme is selected.
    /// </summary>
    public bool IsLightTheme
    {
        get => _themeMode == AppTheme.Light;
        set { if (value) ThemeMode = AppTheme.Light; }
    }

    /// <summary>
    /// Gets whether dark theme is selected.
    /// </summary>
    public bool IsDarkTheme
    {
        get => _themeMode == AppTheme.Dark;
        set { if (value) ThemeMode = AppTheme.Dark; }
    }
}
