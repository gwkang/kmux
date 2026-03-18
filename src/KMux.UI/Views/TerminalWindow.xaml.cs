using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KMux.Core.Models;
using KMux.UI.ViewModels;

namespace KMux.UI.Views;

public partial class TerminalWindow : Window
{
    public static readonly DependencyProperty AssetsPathProperty =
        DependencyProperty.Register(nameof(AssetsPath), typeof(string),
            typeof(TerminalWindow), new PropertyMetadata(null));

    public string? AssetsPath
    {
        get => (string?)GetValue(AssetsPathProperty);
        set => SetValue(AssetsPathProperty, value);
    }

    private TerminalWindowViewModel? VM => DataContext as TerminalWindowViewModel;

    public TerminalWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            PaneContainer.Tab = null;   // evicts & disposes all TerminalPane/WebView2Bridge instances
            (DataContext as IDisposable)?.Dispose();
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TerminalWindowViewModel vm)
                vm.WindowCloseRequested += (_, _) => Close();
        };
    }

    // ── Tab bar events ───────────────────────────────────────────────────────

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TabViewModel tab })
        {
            if (VM is null) return;
            VM.ActiveTab = tab;
            foreach (var t in VM.Tabs) t.IsActive = t == tab;
        }
    }

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TabViewModel tab })
            VM?.CloseTabCommand.Execute(tab);
        e.Handled = true;
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
        => VM?.NewTabCommand.Execute(null);

    private void SplitHButton_Click(object sender, RoutedEventArgs e)
        => VM?.SplitHorizontalCommand.Execute(null);

    private void SplitVButton_Click(object sender, RoutedEventArgs e)
        => VM?.SplitVerticalCommand.Execute(null);

    private void RecordButton_Click(object sender, RoutedEventArgs e)
        => _ = VM?.ToggleRecordingCommand.ExecuteAsync(null);

    private void RecentFoldersButton_Click(object sender, RoutedEventArgs e)
    {
        if (VM is null) return;
        var dlg = new RecentFoldersWindow(VM.RecentDirStore) { Owner = this };
        dlg.ShowDialog();
        _ = VM.RefreshRecentDirectoriesAsync();
    }

    private void SessionButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SessionManagerWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void MacroButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MacroManagerWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void NewWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var vm  = new TerminalWindowViewModel();
        var win = new TerminalWindow
        {
            AssetsPath  = AssetsPath,
            DataContext = vm,
        };
        win.Show();
    }

    // ── Recent-folder context menus ──────────────────────────────────────────

    private void NewTab_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || VM is null) return;
        var dirs = GetDirsWithCurrent(VM.RecentDirectories).ToList();

        var menu = new ContextMenu();
        menu.Items.Add(BuildFolderSubmenu("새 탭으로 열기", dirs,
            dir => VM.NewTabCommand.Execute(dir),
            () => { if (BrowseForDirectory("새 탭 폴더 선택") is string d) VM.NewTabCommand.Execute(d); }));
        menu.Items.Add(BuildFolderSubmenu("Claude 탭으로 열기", dirs,
            dir => VM.NewClaudeTabCommand.Execute(dir),
            () => { if (BrowseForDirectory("Claude 탭 폴더 선택") is string d) VM.NewClaudeTabCommand.Execute(d); }));
        fe.ContextMenu = menu;
    }

    private void NewWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || VM is null) return;
        var dirs = GetDirsWithCurrent(VM.RecentDirectories).ToList();

        var menu = new ContextMenu();
        menu.Items.Add(BuildFolderSubmenu("새 창으로 열기", dirs,
            OpenNewWindow,
            () => { if (BrowseForDirectory("새 창 폴더 선택") is string d) OpenNewWindow(d); }));
        menu.Items.Add(BuildFolderSubmenu("Claude 창으로 열기", dirs,
            OpenNewClaudeWindow,
            () => { if (BrowseForDirectory("Claude 창 폴더 선택") is string d) OpenNewClaudeWindow(d); }));
        fe.ContextMenu = menu;
    }

    private IEnumerable<string> GetDirsWithCurrent(IEnumerable<string> recentDirs)
    {
        var activePane = VM?.ActiveTab?.GetPane(VM.ActiveTab.ActivePaneId);
        var currentDir = activePane?.WorkingDirectory;

        var list = recentDirs.ToList();
        if (!string.IsNullOrEmpty(currentDir) &&
            (list.Count == 0 || !string.Equals(list[0], currentDir, StringComparison.OrdinalIgnoreCase)))
        {
            list.Insert(0, currentDir);
        }
        return list;
    }

    private ContextMenu BuildRecentDirMenu(IEnumerable<string> recentDirs,
                                           Action<string>       onDirClick,
                                           Action               onBrowse)
    {
        var menu = new ContextMenu();
        var dirs = recentDirs.ToList();

        if (dirs.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(No recent folders)", IsEnabled = false });
        }
        else
        {
            foreach (var dir in dirs)
            {
                var item = new MenuItem { Header = dir };
                item.Click += (_, _) => onDirClick(dir);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator());
        var browse = new MenuItem { Header = "Browse..." };
        browse.Click += (_, _) => onBrowse();
        menu.Items.Add(browse);

        return menu;
    }

    private string? BrowseForDirectory(string title)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = title };
        return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
    }

    private void OpenNewWindow(string workingDir)
    {
        if (VM is null) return;
        OpenWindow(VM.DefaultProfile.WithWorkingDir(workingDir));
    }

    private void OpenNewClaudeWindow(string workingDir)
        => OpenWindow(ShellProfile.ClaudeCode.WithWorkingDir(workingDir));

    private void OpenWindow(ShellProfile profile)
    {
        var vm  = new TerminalWindowViewModel(profile);
        var win = new TerminalWindow { AssetsPath = AssetsPath, DataContext = vm };
        win.Show();
    }

    /// <summary>Shows a KMux context menu over the given terminal pane with folder-picker options.</summary>
    public void ShowPaneContextMenu(FrameworkElement target)
    {
        if (VM is null) return;
        var dirs = GetDirsWithCurrent(VM.RecentDirectories).ToList();

        var menu = new ContextMenu();
        menu.Items.Add(BuildFolderSubmenu("새 탭으로 열기", dirs,
            dir => VM.NewTabCommand.Execute(dir),
            () => { if (BrowseForDirectory("새 탭 폴더 선택") is string d) VM.NewTabCommand.Execute(d); }));
        menu.Items.Add(BuildFolderSubmenu("새 창으로 열기", dirs,
            OpenNewWindow,
            () => { if (BrowseForDirectory("새 창 폴더 선택") is string d) OpenNewWindow(d); }));
        menu.Items.Add(new Separator());
        menu.Items.Add(BuildFolderSubmenu("Claude 탭으로 열기", dirs,
            dir => VM.NewClaudeTabCommand.Execute(dir),
            () => { if (BrowseForDirectory("Claude 탭 폴더 선택") is string d) VM.NewClaudeTabCommand.Execute(d); }));
        menu.Items.Add(BuildFolderSubmenu("Claude 창으로 열기", dirs,
            OpenNewClaudeWindow,
            () => { if (BrowseForDirectory("Claude 창 폴더 선택") is string d) OpenNewClaudeWindow(d); }));

        menu.PlacementTarget = target;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static MenuItem BuildFolderSubmenu(string header, IList<string> dirs,
                                               Action<string> onDirClick, Action onBrowse)
    {
        var parent = new MenuItem { Header = header };

        if (dirs.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = "(No recent folders)", IsEnabled = false });
        }
        else
        {
            foreach (var dir in dirs)
            {
                var item = new MenuItem { Header = dir };
                item.Click += (_, _) => onDirClick(dir);
                parent.Items.Add(item);
            }
        }

        parent.Items.Add(new Separator());
        var browse = new MenuItem { Header = "폴더 선택..." };
        browse.Click += (_, _) => onBrowse();
        parent.Items.Add(browse);

        return parent;
    }

    // ── Keyboard handling ────────────────────────────────────────────────────

    /// <summary>Called by TerminalPane when xterm.js intercepts a KMux key (WebView2 has Win32 focus).</summary>
    public void HandleAcceleratorKey(Key key, ModifierKeys mods)
    {
        if (VM?.HandleKey(key, mods) == true)
            UpdateRecordingIndicator();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (VM is null) return;

        var consumed = VM.HandleKey(e.Key == Key.System ? e.SystemKey : e.Key,
                                    Keyboard.Modifiers);
        if (consumed)
        {
            e.Handled = true;
            UpdateRecordingIndicator();
        }
    }

    private void UpdateRecordingIndicator()
    {
        if (VM is null) return;
        RecordingIndicator.Visibility = VM.IsRecording
            ? Visibility.Visible : Visibility.Collapsed;
        RecordButton.Foreground = VM.IsRecording
            ? new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(243, 139, 168))
            : new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(166, 173, 200));
    }
}
