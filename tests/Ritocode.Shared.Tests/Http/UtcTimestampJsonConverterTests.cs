using System.Text.Json;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Tests.Http;

/// <summary>ADR 0003: timestamps are ISO 8601 in UTC with an explicit Z.</summary>
public sealed class UtcTimestampJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new UtcTimestampJsonConverter() } };

    [Fact]
    public void AUtcTimestamp_IsWrittenWithAZ()
    {
        var value = new DateTimeOffset(2026, 9, 30, 15, 7, 41, TimeSpan.Zero).AddTicks(6048470);

        Assert.Equal("\"2026-09-30T15:07:41.604847Z\"", JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void AnOffsetTimestamp_IsWrittenAsTheSameInstantInUtc()
    {
        var value = new DateTimeOffset(2026, 9, 30, 19, 7, 41, TimeSpan.FromHours(4));

        Assert.Equal("\"2026-09-30T15:07:41Z\"", JsonSerializer.Serialize(value, Options));
    }

    [Fact]
    public void AWrittenTimestamp_ReadsBackAsTheSameInstant()
    {
        var value = new DateTimeOffset(2026, 9, 30, 15, 7, 41, 123, TimeSpan.Zero);

        Assert.Equal(value, JsonSerializer.Deserialize<DateTimeOffset>(JsonSerializer.Serialize(value, Options), Options));
    }
}
