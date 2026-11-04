namespace Ritocode.Modules.Users.Admin;

/// <summary>
/// Who may open the admin area: users named here by e-mail (docs/SPEC.md §6.2). There is no role
/// screen; adding an admin is a configuration change and a restart.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Users:Admin";

    /// <summary>The admins' addresses, compared with the stored address ignoring case and surrounding space.</summary>
    public IReadOnlyList<string> Emails { get; init; } = [];

    /// <summary>Whether <paramref name="email"/>, as the Users module stores it (lower-cased), names an admin.</summary>
    public bool Names(string email) =>
        Emails.Any(admin => !string.IsNullOrWhiteSpace(admin) && string.Equals(admin.Trim(), email, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every entry is an address, or blank. A blank entry names nobody: the production Compose file
    /// passes <c>ADMIN_EMAIL</c> through even when it is left empty.
    /// </summary>
    public bool IsValid() =>
        Emails.All(email => string.IsNullOrWhiteSpace(email) || email.Contains('@', StringComparison.Ordinal));
}
