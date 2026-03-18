using System.Text.Json.Serialization;
using KMux.Layout;

namespace KMux.Macro;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KeyInputAction),  "key")]
[JsonDerivedType(typeof(PasteAction),     "paste")]
[JsonDerivedType(typeof(DelayAction),     "delay")]
[JsonDerivedType(typeof(ResizeAction),    "resize")]
[JsonDerivedType(typeof(NewPaneAction),   "newPane")]
[JsonDerivedType(typeof(SwitchTabAction), "switchTab")]
[JsonDerivedType(typeof(RunCommandAction),"runCommand")]
public abstract record MacroAction;

/// <summary>Raw key input (VT sequence or printable char)</summary>
public sealed record KeyInputAction(string Data, long TimestampMs) : MacroAction;

/// <summary>Paste a block of text instantly</summary>
public sealed record PasteAction(string Text) : MacroAction;

/// <summary>Wait N milliseconds</summary>
public sealed record DelayAction(int Ms) : MacroAction;

/// <summary>Resize the terminal to cols×rows</summary>
public sealed record ResizeAction(short Cols, short Rows) : MacroAction;

/// <summary>Split active pane</summary>
public sealed record NewPaneAction(SplitDirection Direction) : MacroAction;

/// <summary>Switch to tab by zero-based index</summary>
public sealed record SwitchTabAction(int Index) : MacroAction;

/// <summary>Run a shell command (shorthand for typing + Enter)</summary>
public sealed record RunCommandAction(string Command) : MacroAction;
