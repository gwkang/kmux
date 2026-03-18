using System.Text.Json;
using System.Text.Json.Serialization;
using KMux.Core.Models;

namespace KMux.Session;

public class SessionStore
{
    private readonly string _baseDir;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented          = true,
        PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() }
    };

    public SessionStore(string? baseDir = null)
    {
        _baseDir = baseDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "KMux", "sessions");
        Directory.CreateDirectory(_baseDir);
    }

    public async Task SaveAsync(KMux.Core.Models.Session session)
    {
        session.SavedAt = DateTime.UtcNow;
        var path = GetPath(session.Id);
        var json = JsonSerializer.Serialize(session, Options);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<KMux.Core.Models.Session?> LoadAsync(Guid id)
    {
        var path = GetPath(id);
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<KMux.Core.Models.Session>(json, Options);
    }

    public async Task<IReadOnlyList<SessionMeta>> ListAsync()
    {
        var metas = new List<SessionMeta>();
        foreach (var file in Directory.GetFiles(_baseDir, "*.json"))
        {
            try
            {
                var json    = await File.ReadAllTextAsync(file);
                var session = JsonSerializer.Deserialize<KMux.Core.Models.Session>(json, Options);
                if (session is not null)
                    metas.Add(new SessionMeta
                    {
                        Id          = session.Id,
                        Name        = session.Name,
                        CreatedAt   = session.CreatedAt,
                        SavedAt     = session.SavedAt,
                        WindowCount = session.Windows.Count
                    });
            }
            catch { /* skip corrupt file */ }
        }
        return metas.OrderByDescending(m => m.SavedAt).ToList();
    }

    public Task DeleteAsync(Guid id)
    {
        var path = GetPath(id);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(Guid id) => Path.Combine(_baseDir, $"{id}.json");
}
