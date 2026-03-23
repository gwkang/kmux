using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using KMux.Core.Interfaces;
using KMux.Core.Models;
using KMux.Macro;
using KMux.Terminal.ConPty;

namespace KMux.UI.ViewModels;

public partial class TerminalViewModel : ObservableObject, IDisposable
{
    public Guid PaneId { get; }

    [ObservableProperty] private string  _title             = "Shell";
    [ObservableProperty] private bool    _isActive;
    [ObservableProperty] private bool    _isClaudeBusy;
    [ObservableProperty] private bool    _isClaudeReady;
    [ObservableProperty] private string? _currentWorkingDir;

    public ITerminalProcess? Process => _process;
    public MacroRecorder    MacroRecorder { get; }

    // OSC 7: shell reports CWD as  ESC ] 7 ; file:///path BEL  or  ESC ] 7 ; file:///path ST
    // PowerShell 7+ emits this automatically on every prompt.
    private static readonly Regex Osc7Regex = new(
        @"\x1b\]7;file://[^/]*/([^\x07\x1b]+)(?:\x07|\x1b\\)",
        RegexOptions.Compiled);

    // Claude Code braille spinner characters
    private static readonly char[] ClaudeSpinnerChars = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];

    private readonly ShellProfile         _profile;
    private          ITerminalProcess?    _process;

    private readonly System.Timers.Timer _idleTimer;
    private readonly System.Timers.Timer _claudeIdleTimer;

    // Called by WebView2Bridge to forward output to xterm.js
    private readonly List<string> _outputBuffer = new();
    private EventHandler<string>? _outputAvailable;

    public event EventHandler<string>? OutputAvailable
    {
        add
        {
            lock (_outputBuffer)
            {
                _outputAvailable += value;
                foreach (var text in _outputBuffer)
                    value?.Invoke(this, text);
                _outputBuffer.Clear();
            }
        }
        remove => _outputAvailable -= value;
    }

    public TerminalViewModel(Guid paneId, ShellProfile profile, MacroRecorder macroRecorder)
    {
        PaneId        = paneId;
        MacroRecorder = macroRecorder;
        _profile      = profile;

        _idleTimer = new System.Timers.Timer(600) { AutoReset = false };
        _idleTimer.Elapsed += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => IsActive = false);

        _claudeIdleTimer = new System.Timers.Timer(30_000) { AutoReset = false };
        _claudeIdleTimer.Elapsed += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsClaudeBusy  = false;
                IsClaudeReady = true;
            });
    }

    // JS → C#: user typed something in xterm
    public void HandleInput(string data)
    {
        MacroRecorder.RecordKeyInput(data);
        _process?.Write(data);
    }

    // JS → C#: terminal was resized
    public void HandleResize(int cols, int rows)
    {
        if (_process is null)
        {
            // First resize from xterm.js — start process with actual terminal size
            _process = ConPtyProcess.Start(_profile, (short)cols, (short)rows);
            _process.OutputReceived += OnOutputReceived;
            _process.Exited         += OnProcessExited;
            return;
        }
        MacroRecorder.RecordResize((short)cols, (short)rows);
        _process.Resize((short)cols, (short)rows);
    }

    private void OnOutputReceived(object? sender, KMux.Core.Events.TerminalDataEventArgs e)
    {
        var text = Encoding.UTF8.GetString(e.Data);

        // Capture handler and buffer under lock, but invoke OUTSIDE the lock.
        // Holding the lock across the invocation caused starvation: the UI thread
        // trying to do  OutputAvailable -= handler  (in WebView2Bridge.Dispose) could
        // never acquire the lock while the pump was calling JSON-serialize + InvokeAsync.
        EventHandler<string>? handler;
        lock (_outputBuffer)
        {
            handler = _outputAvailable;
            if (handler is null) _outputBuffer.Add(text);
        }
        handler?.Invoke(this, text);

        // Parse OSC 7 to track current working directory
        var m = Osc7Regex.Match(text);
        if (m.Success)
        {
            var path = Uri.UnescapeDataString(m.Groups[1].Value)
                          .Replace('/', Path.DirectorySeparatorChar);
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                () => CurrentWorkingDir = path);
        }

        // Detect Claude Code spinner → sustained busy state
        if (text.IndexOfAny(ClaudeSpinnerChars) >= 0)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsClaudeBusy  = true;
                IsClaudeReady = false;
            });
            _claudeIdleTimer.Stop();
            _claudeIdleTimer.Start();
        }

        if (!IsActive)
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => IsActive = true);
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    /// <summary>
    /// Called by <see cref="PaneViewModel"/> when a Claude Code hook reports a definitive state.
    /// Overrides the spinner-based heuristic and resets the idle fallback timer.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetClaudeState(bool busy)
    {
        _claudeIdleTimer.Stop();
        IsClaudeBusy  = busy;
        IsClaudeReady = !busy;
        if (busy) _claudeIdleTimer.Start(); // fallback: clear busy if Stop hook never fires
    }

    public event EventHandler? ProcessExited;

    private void OnProcessExited(object? sender, EventArgs e)
    {
        ProcessExited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        _claudeIdleTimer.Dispose();
        if (_process is not null)
        {
            _process.OutputReceived -= OnOutputReceived;
            _process.Exited         -= OnProcessExited;
            _ = _process.DisposeAsync();
        }
    }
}
