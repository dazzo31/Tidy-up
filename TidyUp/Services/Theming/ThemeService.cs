using System.ComponentModel;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using TidyUp.Models;

namespace TidyUp.Services.Theming;

public interface IThemeService
{
    /// <summary>The theme in effect right now: "Light" or "Dark" (never "System").</summary>
    string EffectiveTheme { get; }

    void Apply(string? theme);

    /// <summary>Applies <paramref name="settings"/>.Theme now and again whenever it changes.</summary>
    void Attach(AppSettings settings);
}

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly ResourceDictionary _resources;
    private readonly Func<ResourceDictionary> _createDarkTokens;
    private readonly Func<bool> _systemUsesLightTheme;
    private readonly Action<Action> _marshal;
    private readonly bool _watchSystemChanges;

    private ResourceDictionary? _darkTokens;
    private AppSettings? _attached;
    private string _requested = AppThemes.System;

    /// <param name="resources">The dictionary holding the MaterialDesign <see cref="BundledTheme"/> (Application.Resources).</param>
    /// <param name="createDarkTokens">Creates the dark overlay for the Win11 colour tokens.</param>
    /// <param name="systemUsesLightTheme">Reads the Windows app-theme preference; defaults to the registry.</param>
    /// <param name="marshal">Runs an action on the UI thread; only used for OS theme change notifications.</param>
    /// <param name="watchSystemChanges">Re-applies the theme when Windows switches between light and dark.</param>
    public ThemeService(
        ResourceDictionary resources,
        Func<ResourceDictionary> createDarkTokens,
        Func<bool>? systemUsesLightTheme = null,
        Action<Action>? marshal = null,
        bool watchSystemChanges = false)
    {
        _resources = resources;
        _createDarkTokens = createDarkTokens;
        _systemUsesLightTheme = systemUsesLightTheme ?? ReadSystemUsesLightTheme;
        _marshal = marshal ?? (action => action());
        _watchSystemChanges = watchSystemChanges;

        if (_watchSystemChanges)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    public string EffectiveTheme { get; private set; } = AppThemes.Light;

    public void Apply(string? theme)
    {
        _requested = AppThemes.Normalize(theme);

        var dark = _requested switch
        {
            AppThemes.Dark => true,
            AppThemes.Light => false,
            _ => !_systemUsesLightTheme()
        };

        var bundled = _resources.MergedDictionaries.OfType<BundledTheme>().FirstOrDefault()
                      ?? throw new InvalidOperationException("No MaterialDesign BundledTheme is merged into the resources.");

        bundled.BaseTheme = dark ? BaseTheme.Dark : BaseTheme.Light;
        SetDarkTokens(dark);
        EffectiveTheme = dark ? AppThemes.Dark : AppThemes.Light;
    }

    public void Attach(AppSettings settings)
    {
        if (_attached != null)
        {
            _attached.PropertyChanged -= OnSettingsChanged;
        }

        _attached = settings;
        settings.PropertyChanged += OnSettingsChanged;
        Apply(settings.Theme);
    }

    /// <summary>Re-reads the Windows preference; only matters while the "System" theme is selected.</summary>
    public void RefreshSystemTheme()
    {
        if (_requested == AppThemes.System)
        {
            Apply(AppThemes.System);
        }
    }

    public void Dispose()
    {
        if (_attached != null)
        {
            _attached.PropertyChanged -= OnSettingsChanged;
            _attached = null;
        }

        if (_watchSystemChanges)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
    }

    private void SetDarkTokens(bool dark)
    {
        if (dark && _darkTokens == null)
        {
            _darkTokens = _createDarkTokens();
            _resources.MergedDictionaries.Add(_darkTokens);
        }
        else if (!dark && _darkTokens != null)
        {
            _resources.MergedDictionaries.Remove(_darkTokens);
            _darkTokens = null;
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Theme) && sender is AppSettings settings)
        {
            Apply(settings.Theme);
        }
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            _marshal(RefreshSystemTheme);
        }
    }

    private static bool ReadSystemUsesLightTheme() =>
        Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) is not int value || value != 0;
}
