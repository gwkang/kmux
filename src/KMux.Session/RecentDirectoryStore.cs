using System.Text.Json;

namespace KMux.Session;

public class RecentDirectoryStore
{
    private const int MaxDirs = 20;

    private readonly string _filePath;

    public RecentDirectoryStore()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KMux");
        _filePath = Path.Combine(appData, "recent_dirs.json");
    }

    public async Task<List<string>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath)) return new();
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>Adds <paramref name="dir"/> to the front and returns the updated list.</summary>
    public async Task<List<string>> AddAsync(string dir)
    {
        var dirs = await LoadAsync();
        if (!string.IsNullOrWhiteSpace(dir))
        {
            dirs.RemoveAll(d => string.Equals(d, dir, StringComparison.OrdinalIgnoreCase));
            dirs.Insert(0, dir);
            if (dirs.Count > MaxDirs) dirs.RemoveRange(MaxDirs, dirs.Count - MaxDirs);
            await SaveRawAsync(dirs);
        }
        return dirs;
    }

    /// <summary>Removes <paramref name="dir"/> from the list and returns the updated list.</summary>
    public async Task<List<string>> RemoveAsync(string dir)
    {
        var dirs = await LoadAsync();
        dirs.RemoveAll(d => string.Equals(d, dir, StringComparison.OrdinalIgnoreCase));
        await SaveRawAsync(dirs);
        return dirs;
    }

    /// <summary>Clears all recent directories.</summary>
    public async Task ClearAsync()
    {
        await SaveRawAsync(new List<string>());
    }

    private async Task SaveRawAsync(List<string> dirs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(dirs));
    }
}
