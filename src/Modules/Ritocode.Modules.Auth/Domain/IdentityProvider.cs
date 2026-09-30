namespace Ritocode.Modules.Auth.Domain;

/// <summary>External identity providers a Ritocode account can be linked to.</summary>
public enum IdentityProvider
{
    /// <summary>GitHub, through its OAuth app.</summary>
    GitHub = 0,

    /// <summary>Google, through its OAuth client (#7).</summary>
    Google = 1,
}
