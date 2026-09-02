namespace Ritocode.Modules.Users.Domain;

/// <summary>
/// How much the platform trusts a user's actions. Phase 1 only distinguishes a new account from an
/// established one; the reputation model that fills this out is Phase 3
/// (<see href="https://github.com/shoraLBRT/ritocode/issues/66">#66</see>).
/// </summary>
public enum TrustLevel
{
    /// <summary>Newly registered. May solve problems; may not contribute anywhere external.</summary>
    New = 0,

    /// <summary>Has a linked provider account and at least one accepted submission.</summary>
    Established = 1,

    /// <summary>Eligible for real repository contributions once Phase 3 opens them.</summary>
    Trusted = 2,
}
