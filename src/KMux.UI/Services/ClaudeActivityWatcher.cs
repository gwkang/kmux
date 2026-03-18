using System.IO;

namespace KMux.UI.Services;

/// <summary>
/// Watches %TEMP%/kmux-status/*.txt files written by Claude Code hooks
/// and raises <see cref="ActivityChanged"/> whenever a pane's activity text changes.
/// </summary>
public sealed class ClaudeActivityWatcher : IDisposable
{
    public static readonly ClaudeActivityWatcher Instance = new();

    private readonly string _statusDir =
        Path.Combine(Path.GetTempPath(), "kmux-status");

    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<Guid, string> _activities = new();

    public event EventHandler<(Guid PaneId, string Activity)>? ActivityChanged;

    private ClaudeActivityWatcher()
    {
        Directory.CreateDirectory(_statusDir);

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
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(e.Name), out var paneId))
                return;

            // Retry read — the writer may still hold the file open briefly
            string text = "";
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    text = File.ReadAllText(e.FullPath).Trim();
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(10);
                }
            }

            lock (_activities)
            {
                if (_activities.TryGetValue(paneId, out var current) && current == text)
                    return;
                _activities[paneId] = text;
            }

            ActivityChanged?.Invoke(this, (paneId, text));
        }
        catch { /* ignore transient errors */ }
    }

    public string GetActivity(Guid paneId)
    {
        lock (_activities)
            return _activities.TryGetValue(paneId, out var v) ? v : "";
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
