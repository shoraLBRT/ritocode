namespace Ritocode.Api.Admin;

/// <summary>
/// How the admin overview's traffic panel (#306) reads Umami's API. Umami's address is the system page's
/// (<see cref="SystemOptions.UmamiUrl"/>); without it, the token or the site's id, the panel says Umami is not
/// configured.
/// </summary>
public sealed class TrafficOptions
{
    public const string SectionName = "Traffic";

    /// <summary>
    /// A token of an Umami account that can see the site, from Umami's <c>POST /api/auth/login</c>; sent as
    /// <c>Authorization: Bearer</c>. <c>UMAMI_API_TOKEN</c> in <c>production.env</c> (docs/RUNBOOK.md).
    /// </summary>
    public string? UmamiToken { get; init; }

    /// <summary>The site's id in Umami — the same value the web image is built with as <c>UMAMI_WEBSITE_ID</c>.</summary>
    public string? UmamiWebsiteId { get; init; }

    /// <summary>
    /// Umami's own pages as the admin's browser reaches them (<c>https://UMAMI_DOMAIN</c>); the panel links to
    /// the site's dashboard there. Unset: no link.
    /// </summary>
    public Uri? UmamiDashboardUrl { get; init; }
}
