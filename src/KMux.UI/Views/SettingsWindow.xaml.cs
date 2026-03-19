using System.Windows;
using System.Windows.Media;
using KMux.Core.Models;
using KMux.Core.Themes;
using KMux.UI.Infrastructure;
using KMux.UI.Services;

namespace KMux.UI.Views;

public partial class SettingsWindow : Window
{
    private record ThemeItem(string Name, Brush Background, Brush Accent, Brush Green, Brush Red);

    private static readonly string[] FontFamilies =
    [
        "Cascadia Code", "Cascadia Mono", "Consolas",
        "JetBrains Mono", "Fira Code", "Source Code Pro", "Courier New",
    ];

    private static readonly int[] FontSizes = [10, 11, 12, 13, 14, 15, 16, 18, 20];

    private readonly List<ThemeItem> _themeItems;
    private AppSettings _draft = CloneSettings(AppSettingsService.Current);

    public SettingsWindow()
    {
        InitializeComponent();
        _themeItems = BuiltInThemes.All.Select(kv => new ThemeItem(
            Name:       kv.Key,
            Background: ThemeHelper.HexBrush(kv.Value.Base),
            Accent:     ThemeHelper.HexBrush(kv.Value.Accent),
            Green:      ThemeHelper.HexBrush(kv.Value.Green),
            Red:        ThemeHelper.HexBrush(kv.Value.Red)
        )).ToList();
        Loaded += OnLoaded;
        ContentRendered += (_, _) => WindowAnimations.PlayEntry(RootContent, EntryScale);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeList.ItemsSource        = _themeItems;
        FontFamilyCombo.ItemsSource  = FontFamilies;
        FontSizeCombo.ItemsSource    = FontSizes;
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        ThemeList.SelectedItem       = _themeItems.FirstOrDefault(t => t.Name == _draft.ThemeName)
                                    ?? _themeItems.FirstOrDefault();
        FontFamilyCombo.SelectedItem = FontFamilies.FirstOrDefault(f => f == _draft.TerminalFontFamily)
                                    ?? FontFamilies[0];
        FontSizeCombo.SelectedItem   = FontSizes.Contains(_draft.TerminalFontSize)
                                    ? _draft.TerminalFontSize : 14;
        UpdatePreview();
    }

    private void ThemeList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeList.SelectedItem is ThemeItem item) _draft.ThemeName = item.Name;
    }

    private void FontFamilyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FontFamilyCombo.SelectedItem is string family) { _draft.TerminalFontFamily = family; UpdatePreview(); }
    }

    private void FontSizeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FontSizeCombo.SelectedItem is int size) { _draft.TerminalFontSize = size; UpdatePreview(); }
    }

    private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        => _ = AppSettingsService.ApplyAndSaveAsync(CloneSettings(_draft));

    private void OkBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = AppSettingsService.ApplyAndSaveAsync(CloneSettings(_draft));
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdatePreview()
    {
        var ff = new FontFamily(_draft.TerminalFontFamily + ", Consolas, Courier New");
        var fs = (double)_draft.TerminalFontSize;
        PreviewText1.FontFamily = PreviewText2.FontFamily = PreviewText3.FontFamily = ff;
        PreviewText1.FontSize   = PreviewText2.FontSize   = PreviewText3.FontSize   = fs;
    }

    private static AppSettings CloneSettings(AppSettings src) => new()
    {
        ThemeName          = src.ThemeName,
        TerminalFontFamily = src.TerminalFontFamily,
        TerminalFontSize   = src.TerminalFontSize,
    };
}
