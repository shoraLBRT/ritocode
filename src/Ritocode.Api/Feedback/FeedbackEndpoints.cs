using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Feedback;

/// <summary>What the widget sends. <see cref="Page"/> is the path the visitor was on.</summary>
public sealed record FeedbackRequest(string? Message, string? Page, string? CaptchaToken);

/// <summary>The issue the message became.</summary>
public sealed record FeedbackSent(int? Number);

/// <summary>
/// <c>POST /api/v1/feedback</c> (docs/SPEC.md §9.6, ADR 0019): a message from anyone, signed in or not,
/// becomes an issue with the <c>feedback</c> label. Host-level, like the meta endpoints: it belongs to no
/// module and keeps nothing in the database.
/// </summary>
public static class FeedbackEndpoints
{
    public const string EmptyCode = "feedback_empty";
    public const string TooLongCode = "feedback_too_long";
    public const string RateLimitedCode = "feedback_rate_limited";
    public const string NotVerifiedCode = "feedback_not_verified";
    public const string NotConfiguredCode = "feedback_not_configured";
    public const string UnreachableCode = "feedback_unreachable";
    public const string FailedCode = "feedback_failed";

    private const int MaxPageLength = 300;
    private const int MaxTitleLength = 70;

    public static IServiceCollection AddFeedback(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FeedbackOptions>()
            .Bind(configuration.GetSection(FeedbackOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<FeedbackRateLimiter>();
        services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>(http => http.Timeout = TimeSpan.FromSeconds(10));

        var sink = configuration.GetSection(FeedbackOptions.SectionName).Get<FeedbackOptions>()?.Sink ?? FeedbackSink.GitHub;
        if (sink == FeedbackSink.Log)
        {
            services.AddSingleton<IFeedbackIssues, LogFeedbackIssues>();
        }
        else
        {
            services.AddHttpClient<IFeedbackIssues, GitHubFeedbackIssues>(http =>
            {
                http.BaseAddress = new Uri("https://api.github.com/");
                http.Timeout = TimeSpan.FromSeconds(15);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("ritocode-feedback");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            });
        }

        return services;
    }

    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/feedback", SendAsync)
            .WithName("SendFeedback")
            .WithTags("Feedback")
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>201 with the issue's number. Checked in order: the text, the address's limit, the captcha.</summary>
    private static async Task<IResult> SendAsync(
        FeedbackRequest request,
        IOptions<FeedbackOptions> options,
        FeedbackRateLimiter limiter,
        ICaptchaVerifier captcha,
        IFeedbackIssues issues,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var message = request.Message?.Trim() ?? string.Empty;

        if (message.Length == 0)
        {
            return Refuse(new AppError(ErrorType.Validation, EmptyCode, "The message is empty."), context);
        }

        if (message.Length > settings.MaxLength)
        {
            return Refuse(
                new AppError(ErrorType.Validation, TooLongCode, string.Create(CultureInfo.InvariantCulture, $"The message is longer than {settings.MaxLength} characters.")),
                context);
        }

        var address = context.Connection.RemoteIpAddress?.ToString();

        if (!limiter.TryAcquire(address ?? "unknown"))
        {
            return Refuse(AppError.RateLimited(RateLimitedCode, "Too many messages from this address. Try again later."), context);
        }

        if (!await captcha.VerifyAsync(request.CaptchaToken, address, cancellationToken).ConfigureAwait(false))
        {
            return Refuse(AppError.Forbidden(NotVerifiedCode, "The captcha was not passed."), context);
        }

        var issue = new FeedbackIssue(Title(message), Body(message, request.Page, currentUser.Id), settings.Label);
        var result = await issues.CreateAsync(issue, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            FeedbackIssueOutcome.Created => Results.Json(new FeedbackSent(result.Number), statusCode: StatusCodes.Status201Created),
            FeedbackIssueOutcome.NotConfigured => Refuse(
                new AppError(ErrorType.Unavailable, NotConfiguredCode, "Feedback is not set up on this server."), context),
            FeedbackIssueOutcome.Unreachable => Refuse(
                new AppError(ErrorType.Unavailable, UnreachableCode, "The issue tracker could not be reached. Try again later."), context),
            _ => Refuse(new AppError(ErrorType.Unexpected, FailedCode, "The message could not be filed."), context),
        };
    }

    private static IResult Refuse(AppError error, HttpContext context) => ApiProblem.ToResult(error, context);

    /// <summary>The message's first line, cut to a title's length.</summary>
    internal static string Title(string message)
    {
        var firstLine = message.Split('\n', 2)[0].Trim();
        var title = firstLine.Length <= MaxTitleLength ? firstLine : string.Concat(firstLine.AsSpan(0, MaxTitleLength - 1), "…");
        return $"Feedback: {title}";
    }

    /// <summary>
    /// The message inside a code fence longer than any backtick run in it, so nothing a visitor writes
    /// is rendered — no <c>@mention</c> notifies anyone, no image is loaded, no link is made.
    /// </summary>
    internal static string Body(string message, string? page, Guid? userId)
    {
        var longestRun = 0;
        var run = 0;
        foreach (var character in message)
        {
            run = character == '`' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
        }

        var fence = new string('`', Math.Max(3, longestRun + 1));
        var path = page is { Length: > 0 and <= MaxPageLength } && page.StartsWith('/') && !page.Any(char.IsControl)
            ? page
            : "unknown";

        var body = new StringBuilder()
            .Append(fence).Append("text\n").Append(message).Append('\n').Append(fence).Append("\n\n")
            .Append("Page: `").Append(path.Replace("`", string.Empty, StringComparison.Ordinal)).Append("`\n")
            .Append("User: ").Append(userId is { } id ? id.ToString() : "anonymous").Append('\n');

        return body.ToString();
    }
}
