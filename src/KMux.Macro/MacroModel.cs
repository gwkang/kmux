namespace KMux.Macro;

public class MacroModel
{
    public Guid   Id             { get; init; } = Guid.NewGuid();
    public string Name           { get; set;  } = "Macro";
    public string Description    { get; set;  } = "";
    public List<MacroAction> Actions { get; set; } = new();
    public bool   PreserveTimings { get; set; } = true;
    public string? BoundKey      { get; set;  }   // e.g. "Ctrl+B 1"
    public DateTime CreatedAt    { get; init; } = DateTime.UtcNow;
}
