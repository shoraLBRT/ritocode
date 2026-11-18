using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Ritocode.Api.Feedback;

/// <summary>A feedback issue to open.</summary>
public sealed record FeedbackIssue(string Title, string Body, string Label);

/// <summary>How opening the issue went. <see cref="Number"/> is set only when it was opened.</summary>
public enum FeedbackIssueOutcome
{
    Created,
    NotConfigured,
    Unreachable,
    Failed,
}

public sealed record FeedbackIssueResult(FeedbackIssueOutcome Outcome, int? Number = null);

/// <summary>Opens a feedback issue. GitHub in production; a fake in tests; the log on the local stack.</summary>
public interface IFeedbackIssues
{
    Task<FeedbackIssueResult> CreateAsync(FeedbackIssue issue, CancellationToken cancellationToken);
}

/// <summary>
/// <c>POST https://api.github.com/repos/{owner}/{name}/issues</c> as the configured GitHub App
/// (<see cref="GitHubAppCredential"/>) or, with no App, with the configured token.
/// </summary>
internal sealed partial class GitHubFeedbackIssues(
    HttpClient http,
    IOptions<FeedbackOptions> options,
    GitHubAppCredential appCredential,
    ILogger<GitHubFeedbackIssues> logger)
    : IFeedbackIssues
{
    public async Task<FeedbackIssueResult> CreateAsync(FeedbackIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        var settings = options.Value;
        var useApp = settings.GitHubApp.IsSet;

        if (string.IsNullOrWhiteSpace(settings.Repository) || (!useApp && string.IsNullOrWhiteSpace(settings.GitHubToken)))
        {
            return new FeedbackIssueResult(FeedbackIssueOutcome.NotConfigured);
        }

        try
        {
            var token = useApp
                ? await appCredential.GetTokenAsync(http, settings.Repository, settings.GitHubApp, cancellationToken).ConfigureAwait(false)
                : settings.GitHubToken;

            if (token is null)
            {
                return new FeedbackIssueResult(FeedbackIssueOutcome.Failed);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"repos/{settings.Repository}/issues", UriKind.Relative))
            {
                Content = JsonContent.Create(new GitHubIssueRequest(issue.Title, issue.Body, [issue.Label])),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (useApp && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // A revoked installation token: the next message asks for a new one.
                    appCredential.Forget(token);
                }

                LogRefused(logger, (int)response.StatusCode);
                return new FeedbackIssueResult(FeedbackIssueOutcome.Failed);
            }

            var created = await response.Content.ReadFromJsonAsync<GitHubIssueResponse>(cancellationToken).ConfigureAwait(false);
            return new FeedbackIssueResult(FeedbackIssueOutcome.Created, created?.Number);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, exception);
            return new FeedbackIssueResult(FeedbackIssueOutcome.Unreachable);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub refused a feedback issue with {Status}.")]
    private static partial void LogRefused(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub could not be reached to open a feedback issue.")]
    private static partial void LogUnreachable(ILogger logger, Exception exception);

    private sealed record GitHubIssueRequest(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels);

    private sealed record GitHubIssueResponse([property: JsonPropertyName("number")] int Number);
}

/// <summary>The local stack's sink: the issue goes to the log, so the widget can be tried without a token.</summary>
internal sealed partial class LogFeedbackIssues(ILogger<LogFeedbackIssues> logger) : IFeedbackIssues
{
    private int _next;

    public Task<FeedbackIssueResult> CreateAsync(FeedbackIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        LogIssue(logger, issue.Label, issue.Title, issue.Body);
        return Task.FromResult(new FeedbackIssueResult(FeedbackIssueOutcome.Created, Interlocked.Increment(ref _next)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Feedback issue [{Label}] {Title}: {Body}")]
    private static partial void LogIssue(ILogger logger, string label, string title, string body);
}
