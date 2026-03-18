using KMux.Core.Interfaces;

namespace KMux.Macro;

public class MacroPlayer
{
    private ITerminalProcess? _target;
    private CancellationTokenSource? _cts;

    public bool IsPlaying => _cts is not null && !_cts.IsCancellationRequested;

    public event EventHandler? PlaybackCompleted;
    public event EventHandler<Exception>? PlaybackFailed;

    public void SetTarget(ITerminalProcess target) => _target = target;

    public Task PlayAsync(MacroModel macro) => PlayAsync(macro, CancellationToken.None);

    public async Task PlayAsync(MacroModel macro, CancellationToken externalCt)
    {
        if (_target is null) throw new InvalidOperationException("No target terminal set.");

        // Dispose previous CTS before replacing it
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _cts.Token;

        try
        {
            foreach (var action in macro.Actions)
            {
                ct.ThrowIfCancellationRequested();
                await DispatchAsync(action, macro.PreserveTimings, ct);
            }
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { PlaybackFailed?.Invoke(this, ex); }
    }

    public void Stop() => _cts?.Cancel();

    private Task DispatchAsync(MacroAction action, bool preserveTimings, CancellationToken ct) =>
        action switch
        {
            KeyInputAction   k => Exec(() => _target!.Write(k.Data)),
            PasteAction      p => Exec(() => _target!.Write(p.Text)),
            RunCommandAction r => Exec(() => _target!.Write(r.Command + "\r")),
            ResizeAction     r => Exec(() => _target!.Resize(r.Cols, r.Rows)),
            DelayAction      d => preserveTimings ? Task.Delay(d.Ms, ct) : Task.CompletedTask,
            _                  => Task.CompletedTask
        };

    private static Task Exec(Action a) { a(); return Task.CompletedTask; }
}
