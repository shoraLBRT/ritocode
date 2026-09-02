namespace Ritocode.Shared.Diagnostics;

/// <summary>Tags that decide which probe a health check participates in.</summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Marks a check as gating readiness: the host is running but cannot serve traffic without it.
    /// Liveness deliberately runs no checks at all, so a failing dependency never gets the
    /// container killed.
    /// </summary>
    public const string Ready = "ready";
}
