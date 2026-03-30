using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using KMux.Core.Models;
using KMux.Macro;
using KMux.Terminal.ConPty;
using KMux.UI.Services;

namespace KMux.UI.ViewModels;

public partial class PaneViewModel : ObservableObject, IDisposable
{
    public Guid PaneId => Terminal.PaneId;

    [ObservableProperty] private bool   _isFocused;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PaneDisplayName))]
    private string  _paneTitle       = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayActivity))]
    private string  _claudeActivity  = "";
    [ObservableProperty] private string  _displayPath     = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortSessionId))]
    [NotifyPropertyChangedFor(nameof(HasSessionId))]
    private string? _claudeSessionId;

    public string  DisplayActivity  => IsClaudeBusy && string.IsNullOrEmpty(ClaudeActivity) ? "생각 중…" : ClaudeActivity;
    public string? ShortSessionId  => ClaudeSessionId is { Length: >= 8 } s ? s[..8] : ClaudeSessionId;
    public bool    HasSessionId    => !string.IsNullOrEmpty(ClaudeSessionId);
    // PaneTitle when set by user, otherwise falls back to the two-segment breadcrumb for the dashboard
    public string  PaneDisplayName => string.IsNullOrEmpty(PaneTitle) ? BreadcrumbPath : PaneTitle;

    public bool IsActive      => Terminal.IsActive;
    public bool IsClaudeBusy  => Terminal.IsClaudeBusy;
    public bool IsClaudeReady => Terminal.IsClaudeReady;

    public TerminalViewModel Terminal { get; }

    /// <summary>
    /// Returns the shell process's live CWD via Win32 PEB read.
    /// Falls back to the last OSC-7-reported path, then to the profile's initial WorkingDir.
    /// </summary>
    public string WorkingDirectory
    {
        get
        {
            if (Terminal.Process is { IsRunning: true } proc)
            {
                var live = ProcessUtils.GetCurrentDirectory(proc.ProcessId);
                if (!string.IsNullOrEmpty(live)) return live;
            }
            return string.IsNullOrEmpty(Terminal.CurrentWorkingDir)
                ? _initialWorkingDir
                : Terminal.CurrentWorkingDir;
        }
    }

    private readonly string _initialWorkingDir;

    public event EventHandler? ProcessExited;

    public PaneViewModel(Guid paneId, ShellProfile profile, MacroRecorder recorder)
    {
        _initialWorkingDir = profile.WorkingDir;
        Terminal = new TerminalViewModel(paneId, profile, recorder);
        Terminal.PropertyChanged += OnTerminalPropertyChanged;
        Terminal.ProcessExited   += (s, e) => ProcessExited?.Invoke(this, e);

        ClaudeActivityWatcher.Instance.ActivityChanged  += OnActivityChanged;
        ClaudeActivityWatcher.Instance.SessionIdChanged += OnSessionIdChanged;
        ClaudeActivityWatcher.Instance.StateChanged     += OnStateChanged;

        var watcher = ClaudeActivityWatcher.Instance;

        var existingId       = watcher.GetSessionId(paneId);
        var existingActivity = watcher.GetActivity(paneId);
        var existingBusy     = watcher.GetState(paneId);

        if (!string.IsNullOrEmpty(existingId))       _claudeSessionId = existingId;
        if (!string.IsNullOrEmpty(existingActivity)) _claudeActivity  = existingActivity;
        if (existingBusy)                            Terminal.SetClaudeState(true);

        RefreshDisplayPath();
    }

    private void OnActivityChanged(object? sender, (Guid PaneId, string Activity) e)
    {
        if (e.PaneId == PaneId)
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                () => ClaudeActivity = e.Activity);
    }

    private void OnSessionIdChanged(object? sender, (Guid PaneId, string SessionId) e)
    {
        if (e.PaneId == PaneId)
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                () => ClaudeSessionId = e.SessionId);
    }

    private void OnStateChanged(object? sender, (Guid PaneId, bool IsBusy) e)
    {
        if (e.PaneId == PaneId)
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                () => Terminal.SetClaudeState(e.IsBusy));
    }

    /// <summary>Shows the last two path segments (e.g. "kmux/src") for dashboard display.</summary>
    public string BreadcrumbPath => _breadcrumbPath;
    private string _breadcrumbPath = "";

    private void RefreshDisplayPath()
    {
        var path = Terminal.CurrentWorkingDir ?? _initialWorkingDir;
        if (string.IsNullOrEmpty(path))
        {
            DisplayPath = "";
            SetBreadcrumb("");
            return;
        }
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(trimmed))
        {
            DisplayPath = path;
            SetBreadcrumb(path);
            return;
        }
        var parts = trimmed.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                                  StringSplitOptions.RemoveEmptyEntries);
        DisplayPath = parts[^1];
        SetBreadcrumb(parts.Length >= 2 ? $"{parts[^2]}/{parts[^1]}" : parts[^1]);
    }

    private void SetBreadcrumb(string value)
    {
        if (_breadcrumbPath == value) return;
        _breadcrumbPath = value;
        OnPropertyChanged(nameof(BreadcrumbPath));
        // PaneDisplayName falls back to BreadcrumbPath when no custom title is set
        if (string.IsNullOrEmpty(PaneTitle)) OnPropertyChanged(nameof(PaneDisplayName));
    }

    private void OnTerminalPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.IsActive))
            OnPropertyChanged(nameof(IsActive));
        else if (e.PropertyName == nameof(TerminalViewModel.IsClaudeBusy))
        {
            OnPropertyChanged(nameof(IsClaudeBusy));
            OnPropertyChanged(nameof(DisplayActivity));
        }
        else if (e.PropertyName == nameof(TerminalViewModel.IsClaudeReady))
            OnPropertyChanged(nameof(IsClaudeReady));
        else if (e.PropertyName == nameof(TerminalViewModel.CurrentWorkingDir))
        {
            RefreshDisplayPath();
            OnPropertyChanged(nameof(WorkingDirectory));
        }
    }

    public void Dispose()
    {
        ClaudeActivityWatcher.Instance.ActivityChanged  -= OnActivityChanged;
        ClaudeActivityWatcher.Instance.SessionIdChanged -= OnSessionIdChanged;
        ClaudeActivityWatcher.Instance.StateChanged     -= OnStateChanged;
        Terminal.PropertyChanged -= OnTerminalPropertyChanged;
        Terminal.Dispose();
    }
}
