using Microsoft.AspNetCore.WebUtilities;

namespace Ritocode.Modules.Auth.SignIn;

/// <summary>Where a finished sign-in sends the browser.</summary>
public sealed class SignInOptions
{
    public const string SectionName = "Auth:SignIn";

    /// <summary>
    /// The origin the application's pages are served from, when it is not the API's own — in
    /// development, Vite's <c>http://localhost:5173</c>. Empty in production, where Caddy serves both
    /// from one origin and the return path is followed as it is. Configuration, never the request:
    /// the return address a visitor supplies is only ever a path.
    /// </summary>
    public string AppOrigin { get; init; } = string.Empty;
}

/// <summary>One provider's OAuth client, as registered with it. A provider with no client id is not offered.</summary>
public sealed class OAuthProviderOptions
{
    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// The address a sign-in returns to. Accepted only as a local path (docs/SPEC.md §4.6), so the round
/// trip cannot be used to send anyone to another site.
/// </summary>
public static class ReturnUrl
{
    public const string Default = "/";

    /// <summary>
    /// A path on this site: one leading <c>/</c>, not <c>//</c> or <c>/\</c> (which browsers read as
    /// another host), and no control characters. Anything absolute, relative or empty is not local.
    /// </summary>
    public static bool IsLocal(string? value) =>
        !string.IsNullOrEmpty(value)
        && value[0] == '/'
        && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'))
        && !value.Any(char.IsControl);

    /// <summary>The browser's destination: the configured application origin, if any, then the path.</summary>
    public static string Resolve(string appOrigin, string path, string? signInError = null)
    {
        var local = IsLocal(path) ? path : Default;

        if (signInError is not null)
        {
            local = QueryHelpers.AddQueryString(local, "signInError", signInError);
        }

        return string.IsNullOrWhiteSpace(appOrigin) ? local : appOrigin.TrimEnd('/') + local;
    }
}
