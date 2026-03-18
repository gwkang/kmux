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
    public ObservableCollection<string>        RecentDirectories { get; } = new();

    [ObservableProperty] private TabViewModel? _activeTab;

    // Proxy to MacroRecorder so UI can bind to IsRecording without duplicate state
    public bool IsRecording => _recorder.IsRecording;

    public string WindowTitle
    {
        get
        {
            if (ActiveTab is null) return "KMux";
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
        RecentDirectoryStore? recentDirs   = null)
    {
        _defaultProfile = profile      ?? ShellProfile.Cmd;
        _keyMap         = keyMap       ?? KeyBindingMap.CreateDefaults();
        _macroStore     = macroStore   ?? new MacroStore();
        _sessionStore   = sessionStore ?? new SessionStore();
        _recentDirStore = recentDirs   ?? new RecentDirectoryStore();

        NewTab();
        _ = RefreshRecentDirectoriesAsync();
    }

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
        var tab = new TabViewModel(profile, _recorder) { Title = $"{titlePrefix} {Tabs.Count + 1}" };
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
        if (tab is null) return;

        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        tab.Dispose();

        if (Tabs.Count == 0)
        {
            WindowCloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        SetActiveTab(Tabs[Math.Max(0, idx - 1)]);
    }

    public event EventHandler? WindowCloseRequested;

    [RelayCommand]
    public void NextTab()
    {
        if (ActiveTab is null) return;
        SetActiveTab(Tabs[(Tabs.IndexOf(ActiveTab) + 1) % Tabs.Count]);
    }

    [RelayCommand]
    public void PrevTab()
    {
        if (ActiveTab is null) return;
        SetActiveTab(Tabs[(Tabs.IndexOf(ActiveTab) - 1 + Tabs.Count) % Tabs.Count]);
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

    private void SplitAndSave(SplitDirection dir)
    {
        if (ActiveTab is null) return;
        var srcPane = ActiveTab.GetPane(ActiveTab.ActivePaneId);
        var workingDir = srcPane?.WorkingDirectory;
        ActiveTab.SplitPane(ActiveTab.ActivePaneId, dir);
        if (!string.IsNullOrEmpty(workingDir))
            _ = AddToRecentDirsAsync(workingDir);
    }

    [RelayCommand]
    public void CloseActivePane() => ActiveTab?.ClosePane(ActiveTab.ActivePaneId);

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
        }
    }

    public event EventHandler? MacroManagerRequested;
    public event EventHandler? SessionManagerRequested;

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
                    ActiveTab = ActiveTab is null ? 0 : Tabs.IndexOf(ActiveTab),
                    Tabs      = Tabs.Select(t => new TabLayout
                    {
                        Id           = t.Id,
                        Title        = t.Title,
                        RootPaneJson = KMux.Layout.LayoutSerializer.Serialize(t.LayoutRoot)
                    }).ToList()
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
        foreach (var t in Tabs) t.Dispose();
    }
}
