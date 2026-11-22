using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Errors;

/// <summary>The <c>Errors</c> section: retention, the browser endpoint's limits and the Telegram alert.</summary>
public sealed class ErrorsOptions
{
    public const string SectionName = "Errors";

    /// <summary>How long an occurrence is kept (ADR 0020: 30 days). A group stays until it is resolved, and after.</summary>
    public TimeSpan OccurrenceRetention { get; init; } = TimeSpan.FromDays(30);

    /// <summary>The most occurrences kept per group, the latest ones (ADR 0020: 50); the group keeps the count.</summary>
    [Range(1, 10_000)]
    public int OccurrencesPerGroup { get; init; } = 50;

    /// <summary>How often occurrences past their retention are deleted, besides on every new one.</summary>
    public TimeSpan PruneInterval { get; init; } = TimeSpan.FromHours(6);

    public BrowserErrorOptions Browser { get; init; } = new();

    public TelegramOptions Telegram { get; init; } = new();
}

/// <summary>Limits on <c>POST /api/v1/client-errors</c>, which anyone may call.</summary>
public sealed class BrowserErrorOptions
{
    /// <summary>The largest body read; a larger one is refused before it is read.</summary>
    [Range(1024, 1_048_576)]
    public long MaxBodyBytes { get; init; } = 16 * 1024;

    /// <summary>Reports per address in any sliding <see cref="Window"/>.</summary>
    [Range(1, 10_000)]
    public int MaxPerAddress { get; init; } = 10;

    /// <summary>Reports from every address together in any sliding <see cref="Window"/>, so many addresses cannot fill the table.</summary>
    [Range(1, 100_000)]
    public int MaxTotal { get; init; } = 120;

    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// The bot that tells the maintainer of a new or reopened group (ADR 0020 decision 4): a dedicated one, its
/// token and chat id in <c>production.env</c>. Without both, nothing is sent.
/// </summary>
public sealed class TelegramOptions
{
    public string BotToken { get; init; } = string.Empty;

    public string ChatId { get; init; } = string.Empty;

    /// <summary>Telegram's Bot API; a test points it at a fake.</summary>
    public Uri ApiBaseUrl { get; init; } = new("https://api.telegram.org/");

    /// <summary>The site's address, so a message links to <c>/admin/errors</c>; the path alone without it.</summary>
    public string SiteOrigin { get; init; } = string.Empty;

    /// <summary>The most messages per hour; a flood of new groups past it is logged, not sent.</summary>
    [Range(1, 1000)]
    public int MaxPerHour { get; init; } = 20;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(ChatId);
}
