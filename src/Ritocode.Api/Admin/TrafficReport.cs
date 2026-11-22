namespace Ritocode.Api.Admin;

/// <summary>
/// <c>GET /api/v1/admin/stats/traffic</c>: the site's traffic over <c>From</c>..<c>To</c> as Umami counted it (#306).
/// Only <see cref="TrafficStatus.Ok"/> carries figures; any other state carries none, and is not an error.
/// </summary>
/// <param name="Status">Whether Umami answered.</param>
/// <param name="From">The first UTC day counted.</param>
/// <param name="To">The last UTC day counted, included.</param>
/// <param name="DashboardUrl">The site's dashboard in Umami's own pages, when its address is configured.</param>
/// <param name="Summary">Visitors, visits and page views, or <see langword="null"/> unless <c>Status</c> is <c>ok</c>.</param>
/// <param name="Referrers">The top referring domains, most first.</param>
/// <param name="Pages">The top pages by path, most first.</param>
/// <param name="Funnel">The product's funnel, every step in order, a step with no event counted as 0.</param>
public sealed record TrafficReport(
    TrafficStatus Status,
    DateOnly From,
    DateOnly To,
    Uri? DashboardUrl,
    TrafficSummary? Summary,
    IReadOnlyList<TrafficCount> Referrers,
    IReadOnlyList<TrafficCount> Pages,
    IReadOnlyList<TrafficCount> Funnel);

/// <param name="Visitors">Unique visitors in the range.</param>
/// <param name="Visits">Visits (Umami's sessions of activity).</param>
/// <param name="PageViews">Page views, route changes included.</param>
public sealed record TrafficSummary(long Visitors, long Visits, long PageViews);

/// <param name="Name">A referring domain, a page's path, or an event's name.</param>
/// <param name="Count">How many Umami counted.</param>
public sealed record TrafficCount(string Name, long Count);

public enum TrafficStatus
{
    Ok,

    /// <summary>Umami's address, the token or the site's id is not set for this deployment; nothing is wrong.</summary>
    NotConfigured,

    /// <summary>Umami did not answer, or answered with something other than its figures.</summary>
    Failing,

    /// <summary>Umami answered 401 or 403: the token is wrong or expired (docs/RUNBOOK.md says how to get one).</summary>
    Refused,
}
