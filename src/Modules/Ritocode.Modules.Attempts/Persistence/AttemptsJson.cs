using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>
/// How an attempt's answer and result are written and read. The same conventions as the API's JSON —
/// camelCase, enums by name — so a stored result reads like the response it was.
/// </summary>
internal static class AttemptsJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"An attempts column held JSON null where {typeof(T).Name} was expected.");
}
