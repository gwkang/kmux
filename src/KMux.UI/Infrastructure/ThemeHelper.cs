using System.Windows;
using System.Windows.Media;

namespace KMux.UI.Infrastructure;

/// <summary>Shared helpers for reading and converting theme colors.</summary>
internal static class ThemeHelper
{
    /// <summary>Returns the resource brush for <paramref name="key"/>, or a new brush from <paramref name="fallback"/>.</summary>
    public static Brush GetBrush(string key, Color fallback)
    {
        if (Application.Current?.Resources[key] is Brush b) return b;
        return new SolidColorBrush(fallback);
    }

    /// <summary>Parses a CSS hex color string and returns a frozen <see cref="SolidColorBrush"/>.</summary>
    public static SolidColorBrush HexBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
