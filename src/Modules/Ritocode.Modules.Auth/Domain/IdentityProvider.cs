namespace Ritocode.Modules.Auth.Domain;

/// <summary>External identity providers a Ritocode account can be linked to.</summary>
public enum IdentityProvider
{
    /// <summary>GitHub. The only provider in Phase 1, and the one Phase 3 pull requests depend on.</summary>
    GitHub = 0,
}
