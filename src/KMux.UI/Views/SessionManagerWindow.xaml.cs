using System.Windows;
using KMux.Core.Models;
using KMux.Session;

namespace KMux.UI.Views;

public partial class SessionManagerWindow : Window
{
    private readonly SessionStore _store = new();

    public SessionManagerWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var sessions = await _store.ListAsync();
        SessionList.ItemsSource = sessions;
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox(
            "Session name:", "Save Session",
            $"Session {DateTime.Now:yyyy-MM-dd HH:mm}");
        if (string.IsNullOrWhiteSpace(name)) return;

        var session = new KMux.Core.Models.Session { Name = name };
        await _store.SaveAsync(session);
        await RefreshAsync();
    }

    private async void LoadBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SessionList.SelectedItem is not SessionMeta meta) return;
        var session = await _store.LoadAsync(meta.Id);
        if (session is null) return;
        MessageBox.Show($"Session '{session.Name}' loaded.\n(Restore logic TBD)", "KMux");
        Close();
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SessionList.SelectedItem is not SessionMeta meta) return;
        var r = MessageBox.Show($"Delete session '{meta.Name}'?", "KMux",
                                MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        await _store.DeleteAsync(meta.Id);
        await RefreshAsync();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
