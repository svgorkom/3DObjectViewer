using System.Windows;
using Microsoft.Win32;
using _3DObjectViewer.Core.Models;

namespace _3DObjectViewer.Services;

/// <summary>
/// Manages application theming with support for light, dark, and system-follow modes.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private AppTheme _currentMode = AppTheme.System;
    private bool _disposed;

    /// <summary>
    /// Raised when the effective theme changes.
    /// </summary>
    public event Action<bool>? ThemeChanged;

    /// <summary>
    /// Gets whether the current effective theme is dark.
    /// </summary>
    public bool IsDarkTheme { get; private set; }

    /// <summary>
    /// Gets or sets the current theme mode.
    /// </summary>
    public AppTheme CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode == value) return;
            _currentMode = value;
            ApplyTheme();
        }
    }

    /// <summary>
    /// Initializes the theme service and applies the initial theme.
    /// </summary>
    public void Initialize()
    {
        // Listen for Windows theme changes
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
        ApplyTheme();
    }

    /// <summary>
    /// Applies the current theme based on the mode setting.
    /// </summary>
    public void ApplyTheme()
    {
        bool shouldBeDark = _currentMode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            AppTheme.System => IsWindowsInDarkMode(),
            _ => false
        };

        if (IsDarkTheme != shouldBeDark)
        {
            IsDarkTheme = shouldBeDark;
            UpdateApplicationTheme(shouldBeDark);
            ThemeChanged?.Invoke(shouldBeDark);
        }
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentMode == AppTheme.System)
        {
            Application.Current?.Dispatcher.BeginInvoke(ApplyTheme);
        }
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false; // Default to light if we can't read the setting
        }
    }

    private static void UpdateApplicationTheme(bool isDark)
    {
        var app = Application.Current;
        if (app is null) return;

        // Remove existing theme dictionaries
        var toRemove = app.Resources.MergedDictionaries
            .Where(d => d.Source?.OriginalString.Contains("Theme") == true)
            .ToList();
        
        foreach (var dict in toRemove)
        {
            app.Resources.MergedDictionaries.Remove(dict);
        }

        // Add the appropriate theme
        var themePath = isDark ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
        var themeDict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
        app.Resources.MergedDictionaries.Add(themeDict);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
    }
}
