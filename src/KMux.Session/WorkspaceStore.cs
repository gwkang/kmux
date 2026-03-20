using System.Text.Json;
using System.Text.Json.Serialization;
using KMux.Core.Models;

namespace KMux.Session;

/// <summary>Persists the last-used window/tab/pane layout to %APPDATA%\KMux\workspace.json.</summary>
public class WorkspaceStore
{
    private readonly string _path;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() }
    };

    public WorkspaceStore(string? path = null)
    {
        var dir = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KMux");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "workspace.json");
    }

    public void Save(KMux.Core.Models.Session workspace)
    {
        workspace.SavedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(workspace, Options);
        File.WriteAllText(_path, json);
    }

    public async Task<KMux.Core.Models.Session?> LoadAsync()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<KMux.Core.Models.Session>(json, Options);
        }
        catch { return null; }
    }
}
