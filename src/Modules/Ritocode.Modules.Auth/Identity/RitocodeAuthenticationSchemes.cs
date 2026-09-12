namespace Ritocode.Modules.Auth.Identity;

/// <summary>Authentication schemes this platform registers.</summary>
public static class RitocodeAuthenticationSchemes
{
    /// <summary>
    /// The seeded development identity from ADR 0005: no credential, one fixed user, switched on by
    /// configuration. The scheme is always registered so that the host has a default one whether or
    /// not it is enabled — with it disabled the handler authenticates nothing, and a protected
    /// endpoint answers 401 rather than the host failing at request time for want of a scheme.
    /// </summary>
    public const string DevelopmentIdentity = "DevelopmentIdentity";
}
