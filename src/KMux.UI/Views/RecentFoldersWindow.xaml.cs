using System.Windows;
using System.Windows.Controls;
using KMux.Session;
using KMux.UI.Infrastructure;

namespace KMux.UI.Views;

public partial class RecentFoldersWindow : Window
{
    private readonly RecentDirectoryStore _store;

    public RecentFoldersWindow(RecentDirectoryStore store)
    {
        _store = store;
        InitializeComponent();
        Loaded += async (_, _) => FolderList.ItemsSource = await _store.LoadAsync();
        ContentRendered += (_, _) => WindowAnimations.PlayEntry(RootContent, EntryScale);
    }

    private async void AddBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "폴더 선택" };
        if (dlg.ShowDialog(this) != true) return;
        FolderList.ItemsSource = await _store.AddAsync(dlg.FolderName);
    }

    private async void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is not string dir) return;
        FolderList.ItemsSource = await _store.RemoveAsync(dir);
    }

    private async void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("최근 폴더 목록을 모두 삭제하시겠습니까?", "KMux",
                                MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        await _store.ClearAsync();
        FolderList.ItemsSource = null;
    }

    private async void ContextRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string dir })
            FolderList.ItemsSource = await _store.RemoveAsync(dir);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
}
