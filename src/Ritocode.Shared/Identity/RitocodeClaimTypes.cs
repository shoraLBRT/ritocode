namespace Ritocode.Shared.Identity;

/// <summary>
/// Claim types this platform issues and reads. One place, because a claim name is a contract
/// between whatever authenticates a request and everything that reads the result.
/// </summary>
public static class RitocodeClaimTypes
{
    /// <summary>
    /// The Ritocode user identifier, as a GUID in its canonical string form.
    /// </summary>
    /// <remarks>
    /// Named explicitly rather than reusing <c>ClaimTypes.NameIdentifier</c>, so that a stage-two
    /// handler maps its token's subject onto this claim in code a reader can find, instead of
    /// relying on a framework's inbound claim mapping to do it invisibly.
    /// </remarks>
    public const string UserId = "ritocode:user_id";

    /// <summary>
    /// The token a state-changing request must repeat in <see cref="CsrfProtectionMiddleware.HeaderName"/>.
    /// Carried only by a principal a browser authenticates with an ambient credential — the session
    /// cookie — because only such a request can be forged from another site.
    /// </summary>
    public const string CsrfToken = "ritocode:csrf";
}
