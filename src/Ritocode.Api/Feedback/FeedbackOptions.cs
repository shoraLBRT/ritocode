using System.ComponentModel.DataAnnotations;

namespace Ritocode.Api.Feedback;

/// <summary>Where a feedback message goes (ADR 0019).</summary>
public enum FeedbackSink
{
    /// <summary>An issue in the repository below, opened with the token below.</summary>
    GitHub,

    /// <summary>A line in the server log and nothing else: the local stack, where no token exists.</summary>
    Log,
}

/// <summary>
/// The feedback widget's settings (docs/SPEC.md §9.6, ADR 0019), bound from the <c>Feedback</c> section.
/// The token and the captcha secret are server secrets: they come from the environment, never from a file
/// in the repository.
/// </summary>
public sealed class FeedbackOptions
{
    public const string SectionName = "Feedback";

    public FeedbackSink Sink { get; init; } = FeedbackSink.GitHub;

    /// <summary><c>owner/name</c> of the repository the issues are opened in.</summary>
    public string? Repository { get; init; }

    /// <summary>A token allowed to open issues in <see cref="Repository"/>. Empty: the endpoint answers 503.</summary>
    public string? GitHubToken { get; init; }

    /// <summary>The label every feedback issue carries.</summary>
    [Required]
    public string Label { get; init; } = "feedback";

    /// <summary>The longest message accepted, in characters.</summary>
    [Range(1, 20_000)]
    public int MaxLength { get; init; } = 4000;

    /// <summary>Messages one address may send in any <see cref="Window"/>.</summary>
    [Range(1, 1000)]
    public int MaxPerWindow { get; init; } = 5;

    public TimeSpan Window { get; init; } = TimeSpan.FromHours(1);

    /// <summary>The Cloudflare Turnstile secret. Empty: no captcha is asked for (the local stack).</summary>
    public string? TurnstileSecretKey { get; init; }
}
