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
    [ObservableProperty] private string _claudeActivity = "";
    [ObservableProperty] private string _displayPath    = "";

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

        ClaudeActivityWatcher.Instance.ActivityChanged += OnActivityChanged;
        RefreshDisplayPath();
    }

    private void OnActivityChanged(object? sender, (Guid PaneId, string Activity) e)
    {
        if (e.PaneId == PaneId)
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                () => ClaudeActivity = e.Activity);
    }

    private void RefreshDisplayPath()
    {
        var path = Terminal.CurrentWorkingDir ?? _initialWorkingDir;
        if (string.IsNullOrEmpty(path)) { DisplayPath = ""; return; }
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        DisplayPath = string.IsNullOrEmpty(trimmed) ? path : Path.GetFileName(trimmed);
    }

    private void OnTerminalPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.IsActive))
            OnPropertyChanged(nameof(IsActive));
        else if (e.PropertyName == nameof(TerminalViewModel.IsClaudeBusy))
            OnPropertyChanged(nameof(IsClaudeBusy));
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
        ClaudeActivityWatcher.Instance.ActivityChanged -= OnActivityChanged;
        Terminal.PropertyChanged -= OnTerminalPropertyChanged;
        Terminal.Dispose();
    }
}
