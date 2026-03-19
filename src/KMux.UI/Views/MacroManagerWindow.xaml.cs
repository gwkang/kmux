using System.Collections.ObjectModel;
using System.Windows;
using KMux.Macro;
using KMux.UI.Infrastructure;

namespace KMux.UI.Views;

public partial class MacroManagerWindow : Window
{
    private readonly MacroStore _store = new();
    private MacroModel? _selected;

    public MacroManagerWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
        ContentRendered += (_, _) => WindowAnimations.PlayEntry(RootContent, EntryScale);
    }

    private async Task RefreshAsync()
    {
        var macros = await _store.LoadAllAsync();
        MacroList.ItemsSource = macros;
    }

    private void MacroList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MacroList.SelectedItem is not MacroModel macro) return;
        _selected = macro;
        MacroNameBox.Text   = macro.Name;
        HotkeyBox.Text      = macro.BoundKey ?? "";
        PreserveTimingsCheck.IsChecked = macro.PreserveTimings;

        // Build display items for the DataGrid
        var items = macro.Actions.Select(a => new ActionDisplayItem(a)).ToList();
        ActionGrid.ItemsSource = items;
    }

    private void PlayBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        MessageBox.Show("Use the Ctrl+B R keybinding or toolbar to play macros\nwith a target terminal active.", "KMux");
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _selected.Name           = MacroNameBox.Text;
        _selected.BoundKey       = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? null : HotkeyBox.Text;
        _selected.PreserveTimings = PreserveTimingsCheck.IsChecked ?? true;
        await _store.SaveAsync(_selected);
        await RefreshAsync();
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var r = MessageBox.Show($"Delete macro '{_selected.Name}'?", "KMux",
                                MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        await _store.DeleteAsync(_selected.Id);
        _selected = null;
        ActionGrid.ItemsSource = null;
        await RefreshAsync();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}

public class ActionDisplayItem
{
    public string ActionType { get; }
    public string Data       { get; }
    public string DelayMs    { get; }

    public ActionDisplayItem(MacroAction action)
    {
        ActionType = action.GetType().Name.Replace("Action", "");
        Data = action switch
        {
            KeyInputAction  k => k.Data.Length > 40 ? k.Data[..40] + "…" : k.Data,
            PasteAction     p => p.Text.Length  > 40 ? p.Text[..40]  + "…" : p.Text,
            ResizeAction    r => $"{r.Cols}×{r.Rows}",
            NewPaneAction   n => n.Direction.ToString(),
            SwitchTabAction s => $"Tab {s.Index}",
            RunCommandAction rc => rc.Command,
            _ => ""
        };
        DelayMs = action is DelayAction d ? $"{d.Ms} ms" : "";
    }
}
