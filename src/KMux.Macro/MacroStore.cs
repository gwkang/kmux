using System.Text.Json;
using System.Text.Json.Serialization;

namespace KMux.Macro;

public class MacroStore
{
    private readonly string _baseDir;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() }
    };

    public MacroStore(string? baseDir = null)
    {
        _baseDir = baseDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "KMux", "macros");
        Directory.CreateDirectory(_baseDir);
    }

    public async Task SaveAsync(MacroModel macro)
    {
        var path = Path.Combine(_baseDir, $"{macro.Id}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(macro, Options));
    }

    public async Task<MacroModel?> LoadAsync(Guid id)
    {
        var path = Path.Combine(_baseDir, $"{id}.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<MacroModel>(await File.ReadAllTextAsync(path), Options);
    }

    public async Task<IReadOnlyList<MacroModel>> LoadAllAsync()
    {
        var result = new List<MacroModel>();
        foreach (var file in Directory.GetFiles(_baseDir, "*.json"))
        {
            try
            {
                var m = JsonSerializer.Deserialize<MacroModel>(
                    await File.ReadAllTextAsync(file), Options);
                if (m is not null) result.Add(m);
            }
            catch { }
        }
        return result.OrderBy(m => m.Name).ToList();
    }

    public Task DeleteAsync(Guid id)
    {
        var path = Path.Combine(_baseDir, $"{id}.json");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
