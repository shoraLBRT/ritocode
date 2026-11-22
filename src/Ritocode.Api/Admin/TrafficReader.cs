using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Ritocode.Api.Admin;

/// <summary>
/// Makes the <see cref="TrafficReport"/> from Umami's API (#306): <c>/stats</c> for the summary and
/// <c>/metrics</c> for referrers, pages (<c>type=path</c>) and events. Host-level and stateless — nothing is
/// stored. Umami not configured, down or refusing the token gives a report saying so, never an exception.
/// </summary>
internal sealed partial class TrafficReader(
    IHttpClientFactory httpClients,
    IOptions<SystemOptions> system,
    IOptions<TrafficOptions> options,
    ILogger<TrafficReader> logger)
{
    public const string UmamiClient = "traffic-umami";

    /// <summary>How many referrers and pages the panel lists.</summary>
    public const int TopCount = 10;

    /// <summary>The product's funnel (docs/SPEC.md §8), in order: the events <c>frontend/src/analytics</c> sends.</summary>
    public static readonly IReadOnlyList<string> FunnelSteps =
        ["task-opened", "step-2-reached", "check-signed-out", "sign-in-completed", "attempt-submitted"];

    public async Task<TrafficReport> ReadAsync(DateOnly first, DateOnly last, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var dashboard = DashboardUrl(settings);

        if (system.Value.UmamiUrl is not { } umami
            || string.IsNullOrWhiteSpace(settings.UmamiToken)
            || string.IsNullOrWhiteSpace(settings.UmamiWebsiteId))
        {
            return Empty(TrafficStatus.NotConfigured, first, last, dashboard);
        }

        // Umami's range is in milliseconds, both ends included: the first day's start to the last day's end.
        var startAt = new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var endAt = new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds() - 1;
        var range = string.Create(CultureInfo.InvariantCulture, $"startAt={startAt}&endAt={endAt}");
        var site = new Uri(umami, $"/api/websites/{Uri.EscapeDataString(settings.UmamiWebsiteId.Trim())}/");

        var http = httpClients.CreateClient(UmamiClient);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.UmamiToken.Trim());

        try
        {
            var stats = GetAsync(http, new Uri(site, $"stats?{range}"), cancellationToken);
            var referrers = GetAsync(http, new Uri(site, $"metrics?{range}&type=referrer&limit={TopCount}"), cancellationToken);
            var pages = GetAsync(http, new Uri(site, $"metrics?{range}&type=path&limit={TopCount}"), cancellationToken);
            var events = GetAsync(http, new Uri(site, $"metrics?{range}&type=event&limit=100"), cancellationToken);

            // All four are awaited before any is read, so none is left running unobserved when another fails.
            await Task.WhenAll(stats, referrers, pages, events).ConfigureAwait(false);

            using var statsBody = await stats.ConfigureAwait(false);
            using var referrersBody = await referrers.ConfigureAwait(false);
            using var pagesBody = await pages.ConfigureAwait(false);
            using var eventsBody = await events.ConfigureAwait(false);

            var counted = Counts(eventsBody).ToDictionary(count => count.Name, count => count.Count, StringComparer.Ordinal);

            return new TrafficReport(
                TrafficStatus.Ok,
                first,
                last,
                dashboard,
                new TrafficSummary(Figure(statsBody, "visitors"), Figure(statsBody, "visits"), Figure(statsBody, "pageviews")),
                [.. Counts(referrersBody).Take(TopCount)],
                [.. Counts(pagesBody).Take(TopCount)],
                [.. FunnelSteps.Select(step => new TrafficCount(step, counted.GetValueOrDefault(step)))]);
        }
        catch (UmamiRefusedException)
        {
            LogRefused(logger);
            return Empty(TrafficStatus.Refused, first, last, dashboard);
        }
        catch (Exception exception) when (
            (exception is HttpRequestException or TaskCanceledException or JsonException or UmamiAnswerException)
            && !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, exception);
            return Empty(TrafficStatus.Failing, first, last, dashboard);
        }
    }

    private static async Task<JsonDocument> GetAsync(HttpClient http, Uri address, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(address, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UmamiRefusedException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UmamiAnswerException($"Umami answered {(int)response.StatusCode}.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A figure of <c>/stats</c>: a number in Umami 3, <c>{"value": n}</c> in Umami 2. Absent counts as 0; any other
    /// shape is not Umami's answer.
    /// </summary>
    private static long Figure(JsonDocument body, string name)
    {
        if (body.RootElement.ValueKind != JsonValueKind.Object || !body.RootElement.TryGetProperty(name, out var figure))
        {
            throw new UmamiAnswerException($"Umami's stats have no '{name}'.");
        }

        if (figure.ValueKind == JsonValueKind.Object && figure.TryGetProperty("value", out var value))
        {
            figure = value;
        }

        return figure.ValueKind == JsonValueKind.Number && figure.TryGetInt64(out var number)
            ? number
            : throw new UmamiAnswerException($"Umami's '{name}' is not a number.");
    }

    /// <summary><c>/metrics</c>: <c>[{"x": name, "y": count}]</c>. A row without a name (a direct visit) is left out.</summary>
    private static IEnumerable<TrafficCount> Counts(JsonDocument body)
    {
        if (body.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new UmamiAnswerException("Umami's metrics are not a list.");
        }

        foreach (var row in body.RootElement.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object
                && row.TryGetProperty("x", out var x) && x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } name
                && row.TryGetProperty("y", out var y) && y.ValueKind == JsonValueKind.Number && y.TryGetInt64(out var count))
            {
                yield return new TrafficCount(name, count);
            }
        }
    }

    private static Uri? DashboardUrl(TrafficOptions settings) =>
        settings.UmamiDashboardUrl is { } dashboard && !string.IsNullOrWhiteSpace(settings.UmamiWebsiteId)
            ? new Uri(dashboard, $"/websites/{Uri.EscapeDataString(settings.UmamiWebsiteId.Trim())}")
            : settings.UmamiDashboardUrl;

    private static TrafficReport Empty(TrafficStatus status, DateOnly first, DateOnly last, Uri? dashboard) =>
        new(status, first, last, dashboard, null, [], [], []);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The traffic panel could not read Umami.")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Umami refused the traffic panel's token (Traffic:UmamiToken).")]
    private static partial void LogRefused(ILogger logger);

#pragma warning disable CA1064, CA1032 // Private signals inside this reader, never thrown out of it.
    private sealed class UmamiRefusedException : Exception;

    private sealed class UmamiAnswerException(string message) : Exception(message);
#pragma warning restore CA1064, CA1032
}
