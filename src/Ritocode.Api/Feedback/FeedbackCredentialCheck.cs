using Microsoft.Extensions.Options;
using Ritocode.Api.Admin;

namespace Ritocode.Api.Feedback;

/// <summary>
/// Whether the feedback GitHub App (ADR 0019) still signs in, for the admin system page (#297): an installation
/// token is asked for — or the cached one, still fresh, is taken — exactly as a message would. The credential is
/// registered only when messages go to GitHub; without it, or without the App, the App is not configured.
/// </summary>
internal sealed class FeedbackCredentialCheck(HttpClient http, IOptions<FeedbackOptions> options, GitHubAppCredential? credential = null)
{
    public async Task<DependencyStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (credential is null || settings.Sink != FeedbackSink.GitHub || !settings.GitHubApp.IsSet || string.IsNullOrWhiteSpace(settings.Repository))
        {
            return DependencyStatus.NotConfigured;
        }

        try
        {
            var token = await credential.GetTokenAsync(http, settings.Repository, settings.GitHubApp, cancellationToken).ConfigureAwait(false);
            return token is null ? DependencyStatus.Failing : DependencyStatus.Ok;
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
        {
            return DependencyStatus.Failing;
        }
    }
}
