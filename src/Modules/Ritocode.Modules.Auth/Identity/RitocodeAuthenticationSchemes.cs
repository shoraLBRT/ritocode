namespace Ritocode.Modules.Auth.Identity;

/// <summary>Authentication schemes this platform registers.</summary>
public static class RitocodeAuthenticationSchemes
{
    /// <summary>
    /// The default: a request carrying the session cookie goes to <see cref="Session"/>, any other to
    /// <see cref="DevelopmentIdentity"/>, which authenticates nothing when switched off, as it is
    /// outside development.
    /// </summary>
    public const string Default = "Ritocode";

    /// <summary>The session cookie of ADR 0012.</summary>
    public const string Session = "Session";

    /// <summary>
    /// The seeded development identity of ADR 0008: no credential, one fixed user, switched on by
    /// configuration. With it disabled the handler authenticates nothing, and a protected endpoint
    /// answers 401.
    /// </summary>
    public const string DevelopmentIdentity = "DevelopmentIdentity";
}
