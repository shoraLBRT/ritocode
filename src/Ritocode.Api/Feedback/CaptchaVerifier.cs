using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Ritocode.Api.Feedback;

/// <summary>Whether a captcha token proves a person sent the form.</summary>
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteAddress, CancellationToken cancellationToken);
}

/// <summary>
/// Cloudflare Turnstile's <c>siteverify</c>. With no secret configured, no captcha is asked for and every
/// message passes — the local stack. An unreachable Turnstile fails closed: the message is refused.
/// </summary>
internal sealed partial class TurnstileCaptchaVerifier(HttpClient http, IOptions<FeedbackOptions> options, ILogger<TurnstileCaptchaVerifier> logger)
    : ICaptchaVerifier
{
    public static readonly Uri SiteVerify = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    public async Task<bool> VerifyAsync(string? token, string? remoteAddress, CancellationToken cancellationToken)
    {
        var secret = options.Value.TurnstileSecretKey;

        if (string.IsNullOrWhiteSpace(secret))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
        if (!string.IsNullOrEmpty(remoteAddress))
        {
            form["remoteip"] = remoteAddress;
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(SiteVerify, content, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode && result?.Success == true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, exception);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile could not be reached to verify a feedback message.")]
    private static partial void LogUnreachable(ILogger logger, Exception exception);

    private sealed record SiteVerifyResponse([property: JsonPropertyName("success")] bool Success);
}
