using System.ComponentModel.DataAnnotations;

namespace Ritocode.Shared.Persistence;

/// <summary>
/// Database connection settings, bound from the <c>Database</c> configuration section and
/// validated at startup so a missing connection string fails the host immediately rather than
/// on the first query.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false, ErrorMessage = "A PostgreSQL connection string is required.")]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Per-command timeout. A query that outlives it is a bug, not a slow query.</summary>
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Retry count for transient failures. Zero disables the retrying execution strategy, which
    /// is what tests and the migration tool want — a retry there hides a real failure.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; init; } = 3;
}
