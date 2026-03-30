using System.Windows.Input;

namespace KMux.Keybindings;

public class KeyBindingMap
{
    // chord → command (single-chord bindings)
    private readonly Dictionary<KeyChord, string> _singles = new();

    // (prefix, second) → command
    private readonly Dictionary<(KeyChord, KeyChord), string> _prefixed = new();

    // Chords that act as prefix keys (Ctrl+B style)
    private readonly HashSet<KeyChord> _prefixes = new();

    public void AddSingle(KeyChord chord, string command)
        => _singles[chord] = command;

    public void AddChord(KeyChord prefix, KeyChord second, string command)
    {
        _prefixes.Add(prefix);
        _prefixed[(prefix, second)] = command;
    }

    public bool IsPrefix(KeyChord chord) => _prefixes.Contains(chord);

    public string? Resolve(KeyChord chord, KeyChord? prefix = null)
    {
        if (prefix is not null)
        {
            _prefixed.TryGetValue((prefix, chord), out var cmd);
            return cmd;
        }
        _singles.TryGetValue(chord, out var single);
        return single;
    }

    public static KeyBindingMap CreateDefaults()
    {
        var map    = new KeyBindingMap();
        var prefix = new KeyChord(Key.B, ModifierKeys.Control);   // Ctrl+B (tmux-style)

        // Split panes
        map.AddChord(prefix, new KeyChord(Key.OemMinus),                "split.horizontal");
        map.AddChord(prefix, new KeyChord(Key.OemBackslash),            "split.vertical");

        // Tabs
        map.AddChord(prefix, new KeyChord(Key.T),                       "tab.new");
        map.AddChord(prefix, new KeyChord(Key.W),                       "tab.close");
        map.AddChord(prefix, new KeyChord(Key.N),                       "tab.next");
        map.AddChord(prefix, new KeyChord(Key.P),                       "tab.prev");
        map.AddChord(prefix, new KeyChord(Key.OemComma),                "tab.rename");

        // Pane navigation
        map.AddChord(prefix, new KeyChord(Key.Up),                      "pane.focus.up");
        map.AddChord(prefix, new KeyChord(Key.Down),                    "pane.focus.down");
        map.AddChord(prefix, new KeyChord(Key.Left),                    "pane.focus.left");
        map.AddChord(prefix, new KeyChord(Key.Right),                   "pane.focus.right");
        map.AddChord(prefix, new KeyChord(Key.X),                       "pane.close");

        // Window
        map.AddChord(prefix, new KeyChord(Key.N, ModifierKeys.Shift),   "window.new");

        // Macros
        map.AddChord(prefix, new KeyChord(Key.R),                       "macro.record.toggle");
        map.AddChord(prefix, new KeyChord(Key.D5),                      "macro.play");
        map.AddChord(prefix, new KeyChord(Key.M),                       "macro.manager");

        // Session
        map.AddChord(prefix, new KeyChord(Key.S),                       "session.save");
        map.AddChord(prefix, new KeyChord(Key.O),                       "session.manager");

        // Help
        map.AddChord(prefix, new KeyChord(Key.OemQuestion),             "help.keybindings");

        // Direct bindings (no prefix)
        map.AddSingle(new KeyChord(Key.Tab,  ModifierKeys.Control),     "tab.next");
        map.AddSingle(new KeyChord(Key.Tab,  ModifierKeys.Control | ModifierKeys.Shift), "tab.prev");
        map.AddSingle(new KeyChord(Key.N,    ModifierKeys.Control | ModifierKeys.Shift), "window.new");
        map.AddSingle(new KeyChord(Key.T,    ModifierKeys.Control | ModifierKeys.Shift), "tab.new");
        map.AddSingle(new KeyChord(Key.W,    ModifierKeys.Control | ModifierKeys.Shift), "tab.close");
        map.AddSingle(new KeyChord(Key.F11),                            "window.fullscreen");

        return map;
    }
}
