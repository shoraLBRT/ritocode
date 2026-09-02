namespace Ritocode.Modules.Auth.Domain;

/// <summary>
/// A link between a Ritocode account and an external identity. Owned by the Auth module.
/// </summary>
public sealed class LinkedAccount
{
    public const int ProviderUserIdMaxLength = 128;
    public const int ProviderLoginMaxLength = 128;

    public Guid Id { get; set; }

    /// <summary>
    /// The Ritocode user. Deliberately not a foreign key: <c>users</c> belongs to another module
    /// and cross-schema constraints would reinstate the coupling the schema split removes.
    /// See docs/adr/0004-persistence-and-migrations.md.
    /// </summary>
    public Guid UserId { get; set; }

    public IdentityProvider Provider { get; set; }

    /// <summary>The provider's immutable identifier, not the login — logins can be renamed.</summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>Last known login at the provider, kept for display only. May be stale.</summary>
    public string ProviderLogin { get; set; } = string.Empty;

    public DateTimeOffset LinkedAt { get; set; }

    public static LinkedAccount Create(
        Guid userId,
        IdentityProvider provider,
        string providerUserId,
        string providerLogin,
        DateTimeOffset linkedAt) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Provider = provider,
            ProviderUserId = providerUserId,
            ProviderLogin = providerLogin,
            LinkedAt = linkedAt.ToUniversalTime(),
        };
}
