using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KMux.Core.Models;
using KMux.Keybindings;
using KMux.Layout;
using KMux.Macro;
using KMux.Session;

namespace KMux.UI.ViewModels;

public partial class TerminalWindowViewModel : ObservableObject, IDisposable
{
    public ObservableCollection<TabViewModel>  Tabs             { get; } = new();
    public ObservableCollection<TabViewModel>  NonDashboardTabs { get; } = new();
    public ObservableCollection<string>        RecentDirectories { get; } = new();

    [ObservableProperty] private TabViewModel? _activeTab;

    // Proxy to MacroRecorder so UI can bind to IsRecording without duplicate state
    public bool IsRecording => _recorder.IsRecording;

    public string WindowTitle
    {
        get
        {
            if (ActiveTab is null || ActiveTab.IsDashboard) return "KMux";
            var pane   = ActiveTab.GetPane(ActiveTab.ActivePaneId);
            var folder = pane?.DisplayPath;
            return string.IsNullOrEmpty(folder)
                ? $"KMux — {ActiveTab.Title}"
                : $"KMux — {ActiveTab.Title} — {folder}";
        }
    }

    private TabViewModel?  _subscribedTab;
    private PaneViewModel? _subscribedPane;

    public ShellProfile DefaultProfile => _defaultProfile;

    private readonly ShellProfile         _defaultProfile;
    private readonly MacroRecorder        _recorder = new();
    private readonly MacroPlayer          _player   = new();
    private readonly MacroStore           _macroStore;
    private readonly SessionStore         _sessionStore;
    private readonly RecentDirectoryStore _recentDirStore;
    internal RecentDirectoryStore RecentDirStore => _recentDirStore;
    private readonly KeyBindingMap        _keyMap;

    private KeyChord? _pendingPrefix;

    public TerminalWindowViewModel(
        ShellProfile?         profile      = null,
        KeyBindingMap?        keyMap       = null,
        MacroStore?           macroStore   = null,
        SessionStore?         sessionStore = null,
        RecentDirectoryStore? recentDirs   = null,
        KMux.Core.Models.WindowLayout? restore = null)
    {
        _defaultProfile = profile      ?? ShellProfile.Cmd;
        _keyMap         = keyMap       ?? KeyBindingMap.CreateDefaults();
        _macroStore     = macroStore   ?? new MacroStore();
        _sessionStore   = sessionStore ?? new SessionStore();
        _recentDirStore = recentDirs   ?? new RecentDirectoryStore();

        Tabs.CollectionChanged += OnTabsCollectionChanged;

        // Dashboard tab is always first
        var dashboard = TabViewModel.CreateDashboard();
        Tabs.Add(dashboard);

        if (restore is not null)
            RestoreFromLayout(restore);
        else
            NewTab();

        _ = RefreshRecentDirectoriesAsync();
    }

    public bool HasNoTabs    => NonDashboardTabs.Count == 0;
    public int  BusyTabCount => NonDashboardTabs.Count(t => t.IsClaudeBusy);

    private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (TabViewModel t in e.OldItems)
                t.PropertyChanged -= OnAnyTabPropertyChanged;
        if (e.NewItems is not null)
            foreach (TabViewModel t in e.NewItems)
                t.PropertyChanged += OnAnyTabPropertyChanged;
        SyncNonDashboardTabs();
    }

    private void OnAnyTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.IsClaudeBusy))
            OnPropertyChanged(nameof(BusyTabCount));
    }

    private void SyncNonDashboardTabs()
    {
        var wasEmpty = NonDashboardTabs.Count == 0;
        NonDashboardTabs.Clear();
        foreach (var t in Tabs)
            if (!t.IsDashboard) NonDashboardTabs.Add(t);
        if (wasEmpty != (NonDashboardTabs.Count == 0))
            OnPropertyChanged(nameof(HasNoTabs));
        OnPropertyChanged(nameof(BusyTabCount));
    }

    private void RestoreFromLayout(KMux.Core.Models.WindowLayout layout)
    {
        foreach (var tabLayout in layout.Tabs)
        {
            LayoutNode? root = null;
            if (!string.IsNullOrEmpty(tabLayout.RootPaneJson))
                root = KMux.Layout.LayoutSerializer.Deserialize(tabLayout.RootPaneJson);

            if (root is null)
            {
                CreateTab(_defaultProfile, tabLayout.Title ?? "Shell");
                continue;
            }

            var paneMap = tabLayout.Panes.ToDictionary(p => p.PaneId);
            var tab = new TabViewModel(
                _defaultProfile, _recorder,
                root, paneMap,
                info => MakeRestoreProfile(info));
            tab.Title = tabLayout.Title ?? "Shell";
            Tabs.Add(tab);

            // Seed session IDs from the saved layout as a fallback for the case
            // where no hooks fired (Claude never executed a tool) before shutdown.
            foreach (var pane in tab.AllPanes)
            {
                if (!paneMap.TryGetValue(pane.PaneId, out var pi)) continue;
                if (!string.IsNullOrEmpty(pi.ClaudeSessionId) && string.IsNullOrEmpty(pane.ClaudeSessionId))
                    pane.ClaudeSessionId = pi.ClaudeSessionId;
                if (!string.IsNullOrEmpty(pi.PaneTitle))
                    pane.PaneTitle = pi.PaneTitle;
            }

            foreach (var pane in tabLayout.Panes.Where(p => !string.IsNullOrEmpty(p.WorkingDir)))
                _ = AddToRecentDirsAsync(pane.WorkingDir);
        }

        if (NonDashboardTabs.Count == 0) { NewTab(); return; }

        var idx = Math.Clamp(layout.ActiveTab, 0, NonDashboardTabs.Count - 1);
        SetActiveTab(NonDashboardTabs[idx]);
    }

    private ShellProfile MakeRestoreProfile(KMux.Core.Models.PaneInfo info)
    {
        var dir = string.IsNullOrEmpty(info.WorkingDir) ? _defaultProfile.WorkingDir : info.WorkingDir;
        if (!string.IsNullOrEmpty(info.ClaudeSessionId))
        {
            var p = ShellProfile.ClaudeCode.WithWorkingDir(dir);
            p.Arguments = $"/k claude --resume {info.ClaudeSessionId}";
            return p;
        }
        return _defaultProfile.WithWorkingDir(dir);
    }

    private static KMux.Core.Models.TabLayout SerializeTab(TabViewModel t) => new()
    {
        Id           = t.Id,
        Title        = t.Title,
        RootPaneJson = KMux.Layout.LayoutSerializer.Serialize(t.LayoutRoot),
        Panes        = t.AllPanes.Select(p => new KMux.Core.Models.PaneInfo
        {
            PaneId          = p.PaneId,
            WorkingDir      = p.WorkingDirectory,
            ClaudeSessionId = p.ClaudeSessionId,
            PaneTitle       = string.IsNullOrEmpty(p.PaneTitle) ? null : p.PaneTitle
        }).ToList()
    };

    /// <summary>Captures the current window state for workspace persistence.</summary>
    public KMux.Core.Models.WindowLayout CaptureLayout(System.Windows.Window window) => new()
    {
        Left      = window.Left,
        Top       = window.Top,
        Width     = window.ActualWidth  > 0 ? window.ActualWidth  : window.Width,
        Height    = window.ActualHeight > 0 ? window.ActualHeight : window.Height,
        ActiveTab = ActiveTab is null ? 0 : NonDashboardTabs.IndexOf(ActiveTab),
        Tabs      = NonDashboardTabs.Select(SerializeTab).ToList()
    };

    // ── Tab Management ───────────────────────────────────────────────────────

    [RelayCommand]
    public void NewTab(string? workingDir = null)
    {
        var profile = string.IsNullOrEmpty(workingDir)
            ? _defaultProfile
            : _defaultProfile.WithWorkingDir(workingDir);
        CreateTab(profile, "Shell");
    }

    [RelayCommand]
    public void NewClaudeTab(string? workingDir = null)
    {
        var profile = ShellProfile.ClaudeCode.WithWorkingDir(
            string.IsNullOrEmpty(workingDir) ? _defaultProfile.WorkingDir : workingDir);
        CreateTab(profile, "Claude");
    }

    private void CreateTab(ShellProfile profile, string titlePrefix)
    {
        var num = NonDashboardTabs.Count + 1;
        var tab = new TabViewModel(profile, _recorder) { Title = $"{titlePrefix} {num}" };
        Tabs.Add(tab);
        SetActiveTab(tab);
        _ = AddToRecentDirsAsync(profile.WorkingDir);
    }

    private async Task AddToRecentDirsAsync(string dir)
    {
        var dirs = await _recentDirStore.AddAsync(dir);
        ApplyRecentDirectories(dirs);
    }

    public async Task RefreshRecentDirectoriesAsync()
    {
        var dirs = await _recentDirStore.LoadAsync();
        ApplyRecentDirectories(dirs);
    }

    private void ApplyRecentDirectories(List<string> dirs)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            RecentDirectories.Clear();
            foreach (var d in dirs) RecentDirectories.Add(d);
        });
    }

    [RelayCommand]
    public void CloseTab(TabViewModel? tab = null)
    {
        tab ??= ActiveTab;
        if (tab is null || tab.IsDashboard) return;

        var realIdx = NonDashboardTabs.IndexOf(tab);
        Tabs.Remove(tab);
        tab.Dispose();

        if (NonDashboardTabs.Count == 0)
        {
            WindowCloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        SetActiveTab(NonDashboardTabs[Math.Max(0, realIdx - 1)]);
    }

    public event EventHandler? WindowCloseRequested;

    [RelayCommand]
    public void ActivateTab(TabViewModel tab) => SetActiveTab(tab);

    public void MoveTab(TabViewModel tab, int newIndex)
    {
        if (tab.IsDashboard) return;
        var oldIndex = Tabs.IndexOf(tab);
        if (oldIndex < 0) return;
        newIndex = Math.Clamp(newIndex, 1, Tabs.Count - 1);
        if (oldIndex == newIndex) return;
        Tabs.Move(oldIndex, newIndex);
    }

    [RelayCommand]
    public void NextTab() => CycleTab(+1);

    [RelayCommand]
    public void PrevTab() => CycleTab(-1);

    private void CycleTab(int direction)
    {
        if (NonDashboardTabs.Count == 0) return;
        if (ActiveTab is null || ActiveTab.IsDashboard)
        {
            SetActiveTab(direction > 0 ? NonDashboardTabs[0] : NonDashboardTabs[^1]);
            return;
        }
        var idx = NonDashboardTabs.IndexOf(ActiveTab);
        SetActiveTab(NonDashboardTabs[(idx + direction + NonDashboardTabs.Count) % NonDashboardTabs.Count]);
    }

    // Only update the two tabs that change, not the whole collection
    private void SetActiveTab(TabViewModel next)
    {
        if (ActiveTab is not null) ActiveTab.IsActive = false;

        // Update WindowTitle subscriptions
        if (_subscribedTab is not null)
            _subscribedTab.PropertyChanged -= OnActiveTabPropertyChanged;
        if (_subscribedPane is not null)
            _subscribedPane.PropertyChanged -= OnActivePanePropertyChanged;

        ActiveTab = next;
        next.IsActive = true;

        _subscribedTab = next;
        next.PropertyChanged += OnActiveTabPropertyChanged;
        SubscribeActivePane();
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.ActivePaneId))
        {
            SubscribeActivePane();
            OnPropertyChanged(nameof(WindowTitle));
        }
        else if (e.PropertyName == nameof(TabViewModel.Title))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    private void SubscribeActivePane()
    {
        if (_subscribedPane is not null)
            _subscribedPane.PropertyChanged -= OnActivePanePropertyChanged;
        _subscribedPane = ActiveTab?.GetPane(ActiveTab.ActivePaneId);
        if (_subscribedPane is not null)
            _subscribedPane.PropertyChanged += OnActivePanePropertyChanged;
    }

    private void OnActivePanePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaneViewModel.DisplayPath))
            OnPropertyChanged(nameof(WindowTitle));
    }

    // ── Pane Management ──────────────────────────────────────────────────────

    [RelayCommand]
    public void SplitHorizontal() => SplitAndSave(SplitDirection.Horizontal);

    [RelayCommand]
    public void SplitVertical() => SplitAndSave(SplitDirection.Vertical);

    private bool ActiveTabIsTerminal => ActiveTab is { IsDashboard: false };

    private void SplitAndSave(SplitDirection dir)
    {
        if (!ActiveTabIsTerminal) return;
        var srcPane = ActiveTab!.GetPane(ActiveTab.ActivePaneId);
        var workingDir = srcPane?.WorkingDirectory;
        ActiveTab.SplitPane(ActiveTab.ActivePaneId, dir);
        if (!string.IsNullOrEmpty(workingDir))
            _ = AddToRecentDirsAsync(workingDir);
    }

    [RelayCommand]
    public void CloseActivePane()
    {
        if (!ActiveTabIsTerminal) return;
        ActiveTab!.ClosePane(ActiveTab.ActivePaneId);
    }

    // ── Macro ────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task ToggleRecording()
    {
        if (_recorder.IsRecording)
        {
            var macro = _recorder.StopRecording($"Macro {DateTime.Now:HH:mm:ss}");
            await _macroStore.SaveAsync(macro);
        }
        else
        {
            _recorder.StartRecording();
        }
        OnPropertyChanged(nameof(IsRecording));
    }

    [RelayCommand]
    public async Task PlayMacro(MacroModel macro)
    {
        if (ActiveTab?.GetPane(ActiveTab.ActivePaneId) is { } pane &&
            pane.Terminal.Process is { } proc)
        {
            _player.SetTarget(proc);
            await _player.PlayAsync(macro);
        }
    }

    // ── Keybinding handler ───────────────────────────────────────────────────

    /// <returns>true if the key was consumed by KMux</returns>
    public bool HandleKey(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys mods)
    {
        var chord = new KeyChord(key, mods);

        if (_pendingPrefix is not null)
        {
            var cmd = _keyMap.Resolve(chord, _pendingPrefix);
            _pendingPrefix = null;
            if (cmd is not null) { ExecuteCommand(cmd); return true; }
            return false;
        }

        if (_keyMap.IsPrefix(chord)) { _pendingPrefix = chord; return true; }

        var directCmd = _keyMap.Resolve(chord);
        if (directCmd is not null) { ExecuteCommand(directCmd); return true; }
        return false;
    }

    private void ExecuteCommand(string command)
    {
        switch (command)
        {
            case "tab.new":             NewTabCommand.Execute(null);            break;
            case "tab.close":           CloseTabCommand.Execute(null);          break;
            case "tab.next":            NextTabCommand.Execute(null);           break;
            case "tab.prev":            PrevTabCommand.Execute(null);           break;
            case "split.horizontal":    SplitHorizontalCommand.Execute(null);   break;
            case "split.vertical":      SplitVerticalCommand.Execute(null);     break;
            case "pane.close":          CloseActivePaneCommand.Execute(null);   break;
            case "macro.record.toggle": _ = ToggleRecordingCommand.ExecuteAsync(null); break;
            case "pane.focus.up":       MoveFocus(NavigationDirection.Up);      break;
            case "pane.focus.down":     MoveFocus(NavigationDirection.Down);    break;
            case "pane.focus.left":     MoveFocus(NavigationDirection.Left);    break;
            case "pane.focus.right":    MoveFocus(NavigationDirection.Right);   break;
            case "macro.manager":       MacroManagerRequested?.Invoke(this, EventArgs.Empty); break;
            case "session.manager":     SessionManagerRequested?.Invoke(this, EventArgs.Empty); break;
            case "help.keybindings":    HelpRequested?.Invoke(this, EventArgs.Empty); break;
        }
    }

    public event EventHandler? MacroManagerRequested;
    public event EventHandler? SessionManagerRequested;
    public event EventHandler? HelpRequested;

    private void MoveFocus(NavigationDirection dir)
    {
        if (ActiveTab is null) return;
        var adjacent = ActiveTab.GetAdjacentPane(dir);
        if (adjacent.HasValue) ActiveTab.FocusPane(adjacent.Value);
    }

    // ── Session Save ─────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveSession(string? name = null)
    {
        var session = new KMux.Core.Models.Session
        {
            Name    = name ?? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}",
            SavedAt = DateTime.UtcNow,
            Windows = new List<WindowLayout>
            {
                new()
                {
                    ActiveTab = ActiveTab is null ? 0 : NonDashboardTabs.IndexOf(ActiveTab),
                    Tabs      = NonDashboardTabs.Select(SerializeTab).ToList()
                }
            }
        };
        await _sessionStore.SaveAsync(session);
    }

    public void Dispose()
    {
        if (_subscribedTab is not null)
            _subscribedTab.PropertyChanged -= OnActiveTabPropertyChanged;
        if (_subscribedPane is not null)
            _subscribedPane.PropertyChanged -= OnActivePanePropertyChanged;
        foreach (var t in Tabs)
        {
            t.PropertyChanged -= OnAnyTabPropertyChanged;
            t.Dispose();
        }
    }
}
