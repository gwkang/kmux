using System.Text.Json.Serialization;
using System.Windows.Input;

namespace KMux.Keybindings;

public record KeyChord(Key Key, ModifierKeys Modifiers = ModifierKeys.None)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}

public class KeyBinding
{
    public string Command     { get; set; } = "";
    public List<KeyChord> Chords { get; set; } = new();
    public string? Description  { get; set; }
}
