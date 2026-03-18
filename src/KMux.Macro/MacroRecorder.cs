using System.Diagnostics;
using KMux.Layout;

namespace KMux.Macro;

public class MacroRecorder
{
    public bool IsRecording { get; private set; }

    private readonly List<MacroAction> _actions = new();
    private long _lastMs;

    public void StartRecording()
    {
        _actions.Clear();
        _lastMs = NowMs();
        IsRecording = true;
    }

    public void RecordKeyInput(string data)
    {
        if (!IsRecording) return;
        var now   = NowMs();
        var gap   = (int)(now - _lastMs);
        _lastMs   = now;
        if (gap > 50 && _actions.Count > 0) _actions.Add(new DelayAction(gap));
        _actions.Add(new KeyInputAction(data, now));
    }

    public void RecordPaste(string text)     { if (IsRecording) _actions.Add(new PasteAction(text)); }
    public void RecordResize(short c, short r) { if (IsRecording) _actions.Add(new ResizeAction(c, r)); }
    public void RecordNewPane(SplitDirection dir) { if (IsRecording) _actions.Add(new NewPaneAction(dir)); }
    public void RecordSwitchTab(int index)   { if (IsRecording) _actions.Add(new SwitchTabAction(index)); }

    public MacroModel StopRecording(string name = "Recorded Macro")
    {
        IsRecording = false;
        return new MacroModel { Name = name, Actions = new List<MacroAction>(_actions) };
    }

    // Precise millisecond timestamp using floating-point to avoid integer division truncation
    private static long NowMs() =>
        (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
}
