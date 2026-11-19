namespace Ritocode.Api.Admin;

/// <summary>What the admin system page (#297) checks beyond the API's own database.</summary>
public sealed class SystemOptions
{
    public const string SectionName = "System";

    /// <summary>
    /// Umami as the API reaches it — inside the production Compose network <c>http://umami:3000</c>. Its
    /// <c>/api/heartbeat</c> is asked. Unset: Umami is shown as not configured.
    /// </summary>
    public Uri? UmamiUrl { get; init; }
}
