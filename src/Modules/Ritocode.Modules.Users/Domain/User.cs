namespace Ritocode.Modules.Users.Domain;

/// <summary>A platform account. Owned by the Users module; no other module maps this table.</summary>
public sealed class User
{
    public const int EmailMaxLength = 320;
    public const int UsernameMaxLength = 39;

    public Guid Id { get; set; }

    /// <summary>
    /// Stored lower-cased. Uniqueness is enforced on the stored value, so callers must normalise
    /// before writing — see <see cref="Create"/>.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Stored lower-cased, for the same reason as <see cref="Email"/>.</summary>
    public string Username { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Experience points. Never negative; recalculated by the Progress module.</summary>
    public int Xp { get; set; }

    public TrustLevel TrustLevel { get; set; }

    /// <summary>
    /// Creates a user with the invariants the database expects: a v7 identifier, normalised
    /// email and username, and a UTC creation timestamp.
    /// </summary>
    public static User Create(string email, string username, DateTimeOffset createdAt) => new()
    {
        // Version 7 is time-ordered, so inserts land at the right-hand edge of the primary key
        // index instead of scattering across it the way v4 does.
        Id = Guid.CreateVersion7(),
        Email = Normalise(email),
        Username = Normalise(username),
        CreatedAt = createdAt.ToUniversalTime(),
        Xp = 0,
        TrustLevel = TrustLevel.New,
    };

    private static string Normalise(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();
}
