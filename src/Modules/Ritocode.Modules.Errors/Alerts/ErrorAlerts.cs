using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Errors.Domain;

namespace Ritocode.Modules.Errors.Alerts;

/// <summary>Why a group is worth a message: it is new, or it was resolved and came back.</summary>
public enum ErrorAlertKind
{
    New,
    Reopened,
}

/// <summary>
/// What a message says about a group: its source, type, location and count — never its message, a stack,
/// a request id or anything a visitor sent besides the type, as Telegram's servers are outside Russia (ADR 0020).
/// </summary>
public sealed record ErrorAlert(ErrorAlertKind Kind, ErrorSource Source, string Type, string Location, long Count);

/// <summary>Takes an alert off the request's path; sending it is the <see cref="ErrorAlertSender"/>'s.</summary>
public interface IErrorAlerts
{
    void Raise(ErrorAlert alert);
}

/// <summary>A bounded queue: a flood of alerts drops the newest rather than holding up a request.</summary>
internal sealed partial class QueuedErrorAlerts(ILogger<QueuedErrorAlerts> logger) : IErrorAlerts
{
    public Channel<ErrorAlert> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<ErrorAlert>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void Raise(ErrorAlert alert)
    {
        if (!Channel.Writer.TryWrite(alert))
        {
            LogDropped(logger, alert.Kind, alert.Type);
        }
    }

    [LoggerMessage(EventId = 4100, Level = LogLevel.Warning, Message = "Error alert dropped, the queue is full: {Kind} {Type}")]
    private static partial void LogDropped(ILogger logger, ErrorAlertKind kind, string type);
}

/// <summary>Sends the queued alerts one by one, at most <see cref="TelegramOptions.MaxPerHour"/> an hour.</summary>
internal sealed partial class ErrorAlertSender(
    QueuedErrorAlerts queue,
    TelegramErrorAlerts telegram,
    IOptions<ErrorsOptions> options,
    TimeProvider clock,
    ILogger<ErrorAlertSender> logger) : BackgroundService
{
    private readonly Queue<DateTimeOffset> _sent = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var alert in queue.Channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            var now = clock.GetUtcNow();
            while (_sent.Count > 0 && _sent.Peek() <= now.AddHours(-1))
            {
                _sent.Dequeue();
            }

            if (_sent.Count >= options.Value.Telegram.MaxPerHour)
            {
                LogOverLimit(logger, alert.Kind, alert.Type);
                continue;
            }

            _sent.Enqueue(now);
            await telegram.SendAsync(alert, stoppingToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning, Message = "Error alert not sent, past the hour's limit: {Kind} {Type}")]
    private static partial void LogOverLimit(ILogger logger, ErrorAlertKind kind, string type);
}

/// <summary>
/// One message to the maintainer's chat through the Bot API's <c>sendMessage</c>, as plain text, so nothing in
/// a type a visitor sent is rendered. A failure is logged, never thrown: an alert is not worth an error of its own.
/// </summary>
internal sealed partial class TelegramErrorAlerts(HttpClient http, IOptions<ErrorsOptions> options, ILogger<TelegramErrorAlerts> logger)
{
    private const int MaxTypeLength = 120;

    public async Task SendAsync(ErrorAlert alert, CancellationToken cancellationToken)
    {
        var settings = options.Value.Telegram;
        if (!settings.IsConfigured)
        {
            LogNotConfigured(logger, alert.Kind, alert.Type);
            return;
        }

        var address = new Uri(settings.ApiBaseUrl, $"bot{settings.BotToken}/sendMessage");
        var body = new { chat_id = settings.ChatId, text = Text(alert, settings.SiteOrigin), disable_web_page_preview = true };

        try
        {
            using var response = await http.PostAsJsonAsync(address, body, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRefused(logger, (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The token is in the address, so the exception's message is not logged — only its type.
            LogUnreachable(logger, exception.GetType().Name);
        }
    }

    /// <summary>The message: what happened, the type, where, how often, and where to read more.</summary>
    internal static string Text(ErrorAlert alert, string siteOrigin)
    {
        var what = alert.Kind == ErrorAlertKind.New ? "New" : "Reopened";
        var source = alert.Source == ErrorSource.Server ? "server" : "browser";
        var type = new string([.. alert.Type.Where(character => !char.IsControl(character)).Take(MaxTypeLength)]);
        var location = new string([.. alert.Location.Where(character => !char.IsControl(character))]);
        var link = string.IsNullOrWhiteSpace(siteOrigin) ? "/admin/errors" : siteOrigin.TrimEnd('/') + "/admin/errors";

        return new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Ritocode: {what.ToLowerInvariant()} {source} error").Append('\n')
            .Append(type).Append('\n')
            .Append(location).Append('\n')
            .Append(CultureInfo.InvariantCulture, $"Occurrences: {alert.Count}").Append('\n')
            .Append(link)
            .ToString();
    }

    [LoggerMessage(EventId = 4102, Level = LogLevel.Information, Message = "Error alert not sent, Telegram is not configured: {Kind} {Type}")]
    private static partial void LogNotConfigured(ILogger logger, ErrorAlertKind kind, string type);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Warning, Message = "Telegram refused the error alert with {Status}")]
    private static partial void LogRefused(ILogger logger, int status);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Warning, Message = "Telegram could not be reached for the error alert: {Failure}")]
    private static partial void LogUnreachable(ILogger logger, string failure);
}
