using System.ComponentModel.DataAnnotations;

namespace Ritocode.Api.Configuration;

/// <summary>
/// Host-level API settings, bound from the <c>Api</c> configuration section and validated at
/// startup so a misconfigured deployment fails immediately rather than on first request.
/// </summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Path segment every module endpoint sits under, e.g. <c>/api/v1</c>.</summary>
    [Required]
    [RegularExpression("^/[a-z0-9/_-]*[a-z0-9]$", ErrorMessage = "BasePath must be a lowercase absolute path, e.g. /api/v1.")]
    public string BasePath { get; init; } = "/api/v1";

    /// <summary>Browser origins allowed to call the API. Empty disables CORS entirely.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Serve the OpenAPI document. Left off outside Development until the API surface is
    /// intentionally published.
    /// </summary>
    public bool EnableOpenApi { get; init; }
}
