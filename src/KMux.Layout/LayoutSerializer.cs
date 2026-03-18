using System.Text.Json;
using System.Text.Json.Serialization;

namespace KMux.Layout;

public static class LayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented            = true,
        PropertyNamingPolicy     = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull,
        Converters               = { new JsonStringEnumConverter() }
    };

    public static string Serialize(LayoutNode root)
        => JsonSerializer.Serialize(root, Options);

    public static LayoutNode? Deserialize(string json)
        => JsonSerializer.Deserialize<LayoutNode>(json, Options);
}
