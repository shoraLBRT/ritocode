using System.Security.Cryptography;

namespace Ritocode.Modules.Auth.Domain;

/// <summary>
/// A signed-in browser (docs/SPEC.md §6.1, ADR 0012): the cookie carries a random token, and this row
/// — found by the token's hash — says whose it is, until when, and whether it was revoked. The token
/// itself is never stored, so a read of the table signs nobody in.
/// </summary>
public sealed class UserSession
{
    /// <summary>Bytes of randomness in a session token and in its CSRF token.</summary>
    private const int TokenBytes = 32;

    private UserSession()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>No foreign key: the user lives in the Users module's schema; the issuer checks the row exists.</summary>
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 of the cookie's token, hex.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>What a state-changing request must repeat in its <c>X-CSRF-Token</c> header.</summary>
    public string CsrfToken { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set by signing out; a revoked session authenticates nothing.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>A new session for <paramref name="userId"/>, and the token its cookie carries — returned once, never kept.</summary>
    public static (UserSession Session, string Token) Start(Guid userId, DateTimeOffset now, TimeSpan lifetime)
    {
        var token = NewToken();

        var session = new UserSession
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            TokenHash = Hash(token),
            CsrfToken = NewToken(),
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };

        return (session, token);
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>The hash a token is looked up by.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(TokenBytes));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
