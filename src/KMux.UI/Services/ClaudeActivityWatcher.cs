using System.IO;

namespace KMux.UI.Services;

/// <summary>
/// Watches %TEMP%/kmux-status/*.txt files written by Claude Code hooks.
/// <para>
/// File naming convention (both written by the hook script):
/// <list type="bullet">
///   <item><c>{paneId}.txt</c> — current tool activity text</item>
///   <item><c>{paneId}-session.txt</c> — Claude Code session ID</item>
/// </list>
/// </para>
/// </summary>
public sealed class ClaudeActivityWatcher : IDisposable
{
    public static readonly ClaudeActivityWatcher Instance = new();

    private readonly string _statusDir =
        Path.Combine(Path.GetTempPath(), "kmux-status");

    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<Guid, string>  _activities = new();
    private readonly Dictionary<Guid, string>  _sessionIds = new();
    private readonly Dictionary<Guid, bool>    _states     = new();

    public event EventHandler<(Guid PaneId, string Activity)>?  ActivityChanged;
    public event EventHandler<(Guid PaneId, string SessionId)>? SessionIdChanged;
    public event EventHandler<(Guid PaneId, bool IsBusy)>?      StateChanged;

    private ClaudeActivityWatcher()
    {
        Directory.CreateDirectory(_statusDir);

        // Pre-load existing status files so PaneViewModels can read them at startup
        // without needing to wait for a hook event to fire.
        foreach (var file in Directory.GetFiles(_statusDir, "*.txt"))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(file);
            try
            {
                if (nameWithoutExt.EndsWith("-session", StringComparison.Ordinal))
                {
                    var guidPart = nameWithoutExt[..^"-session".Length];
                    if (!Guid.TryParse(guidPart, out var paneId)) continue;
                    var id = File.ReadAllText(file).Trim();
                    if (!string.IsNullOrEmpty(id)) _sessionIds[paneId] = id;
                }
                else if (nameWithoutExt.EndsWith("-state", StringComparison.Ordinal))
                {
                    var guidPart = nameWithoutExt[..^"-state".Length];
                    if (!Guid.TryParse(guidPart, out var paneId)) continue;
                    var state = File.ReadAllText(file).Trim();
                    _states[paneId] = state == "busy";
                }
                else if (Guid.TryParse(nameWithoutExt, out var paneId))
                {
                    var activity = File.ReadAllText(file).Trim();
                    if (!string.IsNullOrEmpty(activity)) _activities[paneId] = activity;
                }
            }
            catch { }
        }

        _watcher = new FileSystemWatcher(_statusDir, "*.txt")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(e.Name ?? "");

            if (nameWithoutExt.EndsWith("-session", StringComparison.Ordinal))
            {
                // Session ID file: {paneId}-session.txt
                var guidPart = nameWithoutExt[..^"-session".Length];
                if (!Guid.TryParse(guidPart, out var paneId)) return;
                var id = ReadWithRetry(e.FullPath);

                lock (_sessionIds)
                {
                    if (_sessionIds.TryGetValue(paneId, out var cur) && cur == id) return;
                    _sessionIds[paneId] = id;
                }
                SessionIdChanged?.Invoke(this, (paneId, id));
            }
            else if (nameWithoutExt.EndsWith("-state", StringComparison.Ordinal))
            {
                // State file: {paneId}-state.txt — "busy" or "ready"
                var guidPart = nameWithoutExt[..^"-state".Length];
                if (!Guid.TryParse(guidPart, out var paneId)) return;
                var state  = ReadWithRetry(e.FullPath);
                var isBusy = state == "busy";
                lock (_states) _states[paneId] = isBusy;
                StateChanged?.Invoke(this, (paneId, isBusy));
            }
            else
            {
                // Activity file: {paneId}.txt
                if (!Guid.TryParse(nameWithoutExt, out var paneId)) return;
                var text = ReadWithRetry(e.FullPath);

                lock (_activities)
                {
                    if (_activities.TryGetValue(paneId, out var current) && current == text) return;
                    _activities[paneId] = text;
                }
                ActivityChanged?.Invoke(this, (paneId, text));
            }
        }
        catch { /* ignore transient errors */ }
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try { return File.ReadAllText(path).Trim(); }
            catch (IOException) { Thread.Sleep(10); }
        }
        return "";
    }

    public string GetActivity(Guid paneId)
    {
        lock (_activities)
            return _activities.TryGetValue(paneId, out var v) ? v : "";
    }

    public string GetSessionId(Guid paneId)
    {
        lock (_sessionIds)
            return _sessionIds.TryGetValue(paneId, out var v) ? v : "";
    }

    public bool GetState(Guid paneId)
    {
        lock (_states)
            return _states.TryGetValue(paneId, out var v) && v;
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
