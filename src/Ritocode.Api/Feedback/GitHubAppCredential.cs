using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ritocode.Api.Feedback;

/// <summary>
/// Signs in to GitHub as the feedback App (ADR 0019): a JWT signed with the App's private key is exchanged for
/// an access token of the App's installation on the repository, and that token opens the issue. An installation
/// token lives an hour; one is kept for the whole process until <see cref="RenewBefore"/> its expiry.
/// </summary>
internal sealed partial class GitHubAppCredential(TimeProvider clock, ILogger<GitHubAppCredential> logger) : IDisposable
{
    /// <summary>How long before its expiry a cached installation token is replaced.</summary>
    internal static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

    /// <summary>The JWT is dated this far back, in case the server's clock is ahead of GitHub's.</summary>
    internal static readonly TimeSpan IssuedBack = TimeSpan.FromSeconds(60);

    /// <summary>The JWT's lifetime from now; GitHub accepts at most ten minutes.</summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(9);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private CachedToken? _cached;

    /// <summary>
    /// The installation token for <paramref name="repository"/>, or <see langword="null"/> when GitHub refuses
    /// either call or the key cannot be read (both logged). An unreachable GitHub throws, as any call through
    /// <paramref name="http"/> does.
    /// </summary>
    public async Task<string?> GetTokenAsync(HttpClient http, string repository, FeedbackGitHubAppOptions app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(app);

        if (Fresh(repository) is { } fresh)
        {
            return fresh;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another message may have renewed it while this one waited.
            if (Fresh(repository) is { } renewed)
            {
                return renewed;
            }

            string jwt;
            try
            {
                jwt = CreateJwt(app.AppId!, app.PrivateKey!, clock.GetUtcNow());
            }
            catch (Exception exception) when (exception is CryptographicException or ArgumentException)
            {
                LogBadKey(logger, exception);
                return null;
            }

            using var lookup = Request(HttpMethod.Get, $"repos/{repository}/installation", jwt);
            using var installationResponse = await http.SendAsync(lookup, cancellationToken).ConfigureAwait(false);
            if (!installationResponse.IsSuccessStatusCode)
            {
                LogRefused(logger, "find the App's installation", (int)installationResponse.StatusCode);
                return null;
            }

            var installation = await installationResponse.Content.ReadFromJsonAsync<Installation>(cancellationToken).ConfigureAwait(false);

            using var exchange = Request(HttpMethod.Post, $"app/installations/{installation?.Id}/access_tokens", jwt);
            using var tokenResponse = await http.SendAsync(exchange, cancellationToken).ConfigureAwait(false);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                LogRefused(logger, "issue an installation token", (int)tokenResponse.StatusCode);
                return null;
            }

            var token = await tokenResponse.Content.ReadFromJsonAsync<InstallationToken>(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token?.Token))
            {
                LogRefused(logger, "issue an installation token", (int)tokenResponse.StatusCode);
                return null;
            }

            _cached = new CachedToken(repository, token.Token, token.ExpiresAt);
            return token.Token;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Drops <paramref name="token"/> if it is the cached one, so the next message asks for a new one.</summary>
    public void Forget(string token)
    {
        if (_cached?.Token == token)
        {
            _cached = null;
        }
    }

    public void Dispose() => _gate.Dispose();

    /// <summary>The App's JWT: RS256 over <c>{ iat: now − 60 s, exp: now + 9 min, iss: appId }</c>.</summary>
    internal static string CreateJwt(string appId, string privateKeyPem, DateTimeOffset now)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.Replace("\\n", "\n", StringComparison.Ordinal).Trim());

        var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new JwtHeader("RS256", "JWT")));
        var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new JwtPayload(
            (now - IssuedBack).ToUnixTimeSeconds(),
            (now + Lifetime).ToUnixTimeSeconds(),
            appId.Trim())));

        var unsigned = $"{header}.{payload}";
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url.EncodeToString(signature)}";
    }

    private string? Fresh(string repository) =>
        _cached is { } cached && cached.Repository == repository && clock.GetUtcNow() < cached.ExpiresAt - RenewBefore
            ? cached.Token
            : null;

    private static HttpRequestMessage Request(HttpMethod method, string path, string jwt)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return request;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub refused to {Step} for feedback with {Status}.")]
    private static partial void LogRefused(ILogger logger, string step, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "The feedback GitHub App's private key could not be read.")]
    private static partial void LogBadKey(ILogger logger, Exception exception);

    private sealed record CachedToken(string Repository, string Token, DateTimeOffset ExpiresAt);

    private sealed record JwtHeader(
        [property: JsonPropertyName("alg")] string Algorithm,
        [property: JsonPropertyName("typ")] string Type);

    private sealed record JwtPayload(
        [property: JsonPropertyName("iat")] long IssuedAt,
        [property: JsonPropertyName("exp")] long ExpiresAt,
        [property: JsonPropertyName("iss")] string Issuer);

    private sealed record Installation([property: JsonPropertyName("id")] long Id);

    private sealed record InstallationToken(
        [property: JsonPropertyName("token")] string? Token,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
}
