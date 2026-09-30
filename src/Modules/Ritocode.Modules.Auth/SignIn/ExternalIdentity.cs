using System.Globalization;
using System.Text.Json;
using Ritocode.Modules.Auth.Domain;

namespace Ritocode.Modules.Auth.SignIn;

/// <summary>Who a provider says has just signed in, reduced to what linking needs.</summary>
/// <param name="ProviderUserId">The provider's immutable identifier for the account.</param>
/// <param name="Login">A display name at the provider — a GitHub login, a Google address — never used to identify.</param>
/// <param name="Email">The account's address, if the provider gave one.</param>
/// <param name="EmailVerified">Whether the provider marks <paramref name="Email"/> as verified. Only then is it used to link.</param>
internal sealed record ExternalIdentity(
    IdentityProvider Provider,
    string ProviderUserId,
    string Login,
    string? Email,
    bool EmailVerified)
{
    /// <summary>
    /// GitHub's <c>/user</c> and <c>/user/emails</c>. The address is the <b>primary</b> one, and it
    /// counts as verified only if GitHub says so; a verified secondary address is not taken instead,
    /// so the account is the one the person thinks of as theirs.
    /// </summary>
    public static ExternalIdentity FromGitHub(JsonElement user, JsonElement emails)
    {
        var primary = emails.ValueKind == JsonValueKind.Array
            ? emails.EnumerateArray().FirstOrDefault(entry => Bool(entry, "primary"))
            : default;

        var email = primary.ValueKind == JsonValueKind.Object ? String(primary, "email") : String(user, "email");
        var verified = primary.ValueKind == JsonValueKind.Object && Bool(primary, "verified");

        return new ExternalIdentity(
            IdentityProvider.GitHub,
            Id(user, "id"),
            String(user, "login") ?? string.Empty,
            email,
            verified && email is not null);
    }

    /// <summary>Google's OpenID Connect userinfo: <c>sub</c>, <c>email</c>, <c>email_verified</c>.</summary>
    public static ExternalIdentity FromGoogle(JsonElement userinfo)
    {
        var email = String(userinfo, "email");

        return new ExternalIdentity(
            IdentityProvider.Google,
            Id(userinfo, "sub"),
            email ?? string.Empty,
            email,
            email is not null && Bool(userinfo, "email_verified"));
    }

    private static string Id(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) switch
        {
            true when value.ValueKind == JsonValueKind.Number => value.GetInt64().ToString(CultureInfo.InvariantCulture),
            true when value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString()!,
            _ => throw new InvalidOperationException($"The provider's profile has no '{name}'."),
        };

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    // Google has sent email_verified both as a boolean and as the string "true".
    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && (value.ValueKind == JsonValueKind.True
            || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase)));
}
