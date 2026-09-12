using System.ComponentModel.DataAnnotations;

namespace Ritocode.Shared.Identity;

/// <summary>
/// The seeded development identity ADR 0005 allows in place of a login, bound from the
/// <c>Authentication:DevelopmentIdentity</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// Declared in <c>Ritocode.Shared</c> and bound once by the composition root because two modules
/// need the same values and may not reference each other: the Auth module's authentication scheme
/// asserts this identity, and the Users module keeps a row in <c>users.users</c> that matches it.
/// The identifier is configuration rather than generated, so the asserted claim and the seeded row
/// agree across restarts and across machines.
/// </para>
/// <para>
/// <see cref="Enabled"/> is off unless a configuration says otherwise. Switched on, this
/// authenticates <em>every</em> request as one fixed user, which is a development convenience in
/// Development and an authentication bypass anywhere else — the host logs a warning naming the
/// environment when it is enabled outside Development rather than refusing to start, because the
/// slice is meant to be put in front of people on a deployed host that still has no login.
/// </para>
/// </remarks>
public sealed class DevelopmentIdentityOptions
{
    public const string SectionName = "Authentication:DevelopmentIdentity";

    /// <summary>Whether the development identity scheme authenticates requests.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// The identifier asserted in the <see cref="RitocodeClaimTypes.UserId"/> claim and written to
    /// <c>users.users.id</c>. Fixed rather than generated: a new identifier per start would orphan
    /// every workspace and submission created under the previous one.
    /// </summary>
    public Guid UserId { get; init; } = new("0199aa00-0000-7000-8000-000000000001");

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; init; } = "developer@ritocode.local";

    [Required]
    [StringLength(39, MinimumLength = 1)]
    public string Username { get; init; } = "developer";
}
