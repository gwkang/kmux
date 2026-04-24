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
            {
                vm.WindowCloseRequested  += (_, _) => Close();
                vm.HelpRequested         += (_, _) => OpenHelpWindow();
            }
        };
    }

    // ── Tab bar events ───────────────────────────────────────────────────────

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: TabViewModel tab } && VM is not null)
            VM.ActivateTabCommand.Execute(tab);
    }

    // ── Tab drag-and-drop reordering ─────────────────────────────────────────

    private Point _tabDragStart;
    private TabViewModel? _draggedTab;

    private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TabViewModel { IsDashboard: false, IsRenaming: false } tab })
        {
            _tabDragStart = e.GetPosition(null);
            _draggedTab = tab;
        }
        else
        {
            _draggedTab = null;
        }
    }

    private void TabItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedTab is null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos  = e.GetPosition(null);
        var diff = pos - _tabDragStart;
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        var tab = _draggedTab;
        _draggedTab = null;
        if (sender is FrameworkElement container)
            DragDrop.DoDragDrop(container, tab, DragDropEffects.Move);
    }

    private void TabItem_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TabViewModel)) &&
                    sender is FrameworkElement { DataContext: TabViewModel { IsDashboard: false } }
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void TabItem_Drop(object sender, DragEventArgs e)
    {
        if (VM is null || !e.Data.GetDataPresent(typeof(TabViewModel))) return;
        if (e.Data.GetData(typeof(TabViewModel)) is not TabViewModel sourceTab) return;
        if (sender is not FrameworkElement { DataContext: TabViewModel { IsDashboard: false } targetTab }) return;
        if (sourceTab == targetTab) return;

        var targetIndex = VM.Tabs.IndexOf(targetTab);
        VM.MoveTab(sourceTab, targetIndex);
        e.Handled = true;
    }

    // ── Tab rename ───────────────────────────────────────────────────────────

    private void TabTitle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 &&
            (sender as FrameworkElement)?.DataContext is TabViewModel { IsDashboard: false } tab)
        {
            tab.IsRenaming = true;
            e.Handled = true;
        }
    }

    private void TabTitleEdit_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && sender is TextBox tb && tb.DataContext is TabViewModel tab)
        {
            tb.Text = tab.Title;
            tb.SelectAll();
            tb.Focus();
        }
    }

    private void TabTitleEdit_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is TabViewModel tab)
            CommitTabRename(tab, tb);
    }

    private void TabTitleEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not TabViewModel tab) return;
        if (e.Key == Key.Enter)  { CommitTabRename(tab, tb); e.Handled = true; }
        if (e.Key == Key.Escape) { tab.IsRenaming = false;    e.Handled = true; }
    }

    private static void CommitTabRename(TabViewModel tab, TextBox tb)
    {
        tab.IsRenaming = false;
        if (!string.IsNullOrWhiteSpace(tb.Text))
            tab.Title = tb.Text.Trim();
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

    private void HelpButton_Click(object sender, RoutedEventArgs e) => OpenHelpWindow();

    private void OpenHelpWindow()
    {
        var dlg = new KeybindingsHelpWindow { Owner = this };
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
        var dirs = GetDirsWithCurrent(VM.RecentDirectories);
        fe.ContextMenu = BuildRecentDirMenu(dirs,
            dir => VM.NewClaudeTabCommand.Execute(dir),
            () => { if (BrowseForDirectory("Claude 탭 폴더 선택") is string d) VM.NewClaudeTabCommand.Execute(d); });
    }

    private void NewWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || VM is null) return;
        var dirs = GetDirsWithCurrent(VM.RecentDirectories);
        fe.ContextMenu = BuildRecentDirMenu(dirs,
            OpenNewClaudeWindow,
            () => { if (BrowseForDirectory("Claude 창 폴더 선택") is string d) OpenNewClaudeWindow(d); });
    }

    private IEnumerable<string> GetDirsWithCurrent(IEnumerable<string> recentDirs)
    {
        var activePane = VM?.ActiveTab?.GetPane(VM.ActiveTab.ActivePaneId);
        var currentDir = activePane?.WorkingDirectory;

        var list = recentDirs.ToList();
        if (!string.IsNullOrEmpty(currentDir))
        {
            list.RemoveAll(d => string.Equals(d, currentDir, StringComparison.OrdinalIgnoreCase));
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

    private void OpenNewClaudeWindow(string workingDir)
        => OpenWindow(ShellProfile.ClaudeCode.WithWorkingDir(workingDir));

    private void OpenWindow(ShellProfile profile)
    {
        var vm  = new TerminalWindowViewModel(profile);
        var win = new TerminalWindow { AssetsPath = AssetsPath, DataContext = vm };
        win.Show();
    }

    /// <summary>Shows a KMux context menu over the given terminal pane with copy/paste and folder-picker options.</summary>
    public void ShowPaneContextMenu(FrameworkElement target, Func<Task<string>> getSelection, Action paste)
    {
        if (VM is null) return;
        var dirs = GetDirsWithCurrent(VM.RecentDirectories).ToList();

        var menu = new ContextMenu();

        var copyItem = new MenuItem { Header = "복사" };
        copyItem.Click += async (_, _) =>
        {
            try
            {
                var text = await getSelection();
                if (!string.IsNullOrEmpty(text))
                    System.Windows.Clipboard.SetText(text);
            }
            catch { /* selection unavailable or clipboard inaccessible */ }
        };
        menu.Items.Add(copyItem);

        var pasteItem = new MenuItem { Header = "붙여넣기" };
        pasteItem.Click += (_, _) => paste();
        menu.Items.Add(pasteItem);

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
        {
            UpdateRecordingIndicator();
            DismissCtrlBHint();
        }
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
            DismissCtrlBHint();
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

    private void DismissCtrlBHint()
    {
        if (CtrlBHint.Visibility == Visibility.Visible)
            CtrlBHint.Visibility = Visibility.Collapsed;
    }
}
