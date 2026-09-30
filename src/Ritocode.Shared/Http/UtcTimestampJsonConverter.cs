using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ritocode.Shared.Http;

/// <summary>
/// Writes a <see cref="DateTimeOffset"/> as ISO 8601 in UTC with an explicit <c>Z</c>, as ADR 0003
/// requires — <c>2026-09-30T15:07:41.604847Z</c> rather than the serializer's default
/// <c>+00:00</c>, and the same instant whatever offset the value carried. Reads any ISO 8601 offset.
/// </summary>
public sealed class UtcTimestampJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture));
    }
}
