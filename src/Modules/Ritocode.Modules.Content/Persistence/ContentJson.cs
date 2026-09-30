using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ritocode.Modules.Content.Persistence;

/// <summary>How the content schema's JSON columns are written and read. One set of options, so the two agree.</summary>
internal static class ContentJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"A content column held JSON null where {typeof(T).Name} was expected.");
}
