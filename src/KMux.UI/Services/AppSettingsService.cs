using System.Windows;
using System.Windows.Media;
using KMux.Core.Models;
using KMux.Core.Themes;
using KMux.Session;
using KMux.UI.Infrastructure;

namespace KMux.UI.Services;

/// <summary>Singleton service that owns the current app settings and applies theme changes.</summary>
public static class AppSettingsService
{
    private static readonly SettingsStore _store = new();
    private static AppSettings _current = new();

    public static AppSettings Current => _current;

    /// <summary>Fired on the UI thread after settings are applied.</summary>
    public static event Action? SettingsChanged;

    public static async Task LoadAndApplyAsync()
    {
        _current = await _store.LoadAsync();
        ApplyThemeToResources(BuiltInThemes.GetOrDefault(_current.ThemeName));
    }

    /// <summary>Apply new settings (UI + terminal) and persist them.</summary>
    public static async Task ApplyAndSaveAsync(AppSettings settings)
    {
        _current = settings;
        ApplyThemeToResources(BuiltInThemes.GetOrDefault(settings.ThemeName));
        SettingsChanged?.Invoke();
        await _store.SaveAsync(settings);
    }

    public static ThemeColors CurrentTheme =>
        BuiltInThemes.GetOrDefault(_current.ThemeName);

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static void ApplyThemeToResources(ThemeColors t)
    {
        var res = Application.Current.Resources;
        SetBrush(res, ThemeResourceKeys.Base,     t.Base);
        SetBrush(res, ThemeResourceKeys.Mantle,   t.Mantle);
        SetBrush(res, ThemeResourceKeys.Crust,    t.Crust);
        SetBrush(res, ThemeResourceKeys.Surface0, t.Surface0);
        SetBrush(res, ThemeResourceKeys.Surface1, t.Surface1);
        SetBrush(res, ThemeResourceKeys.Overlay0, t.Overlay0);
        SetBrush(res, ThemeResourceKeys.Subtext0, t.Subtext0);
        SetBrush(res, ThemeResourceKeys.Subtext1, t.Subtext1);
        SetBrush(res, ThemeResourceKeys.Text,     t.Text);
        SetBrush(res, ThemeResourceKeys.Accent,   t.Accent);
        SetBrush(res, ThemeResourceKeys.Green,    t.Green);
        SetBrush(res, ThemeResourceKeys.Red,      t.Red);
    }

    private static void SetBrush(ResourceDictionary res, string key, string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is not Color color) return;
        res[key] = new SolidColorBrush(color);
    }
}
