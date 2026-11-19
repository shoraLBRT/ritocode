namespace Ritocode.Api.Admin;

/// <summary>
/// <c>GET /api/v1/admin/system</c>: what is deployed and how it is (#297). The web image's commit is not here —
/// the browser reads it from the <c>version.json</c> the web image serves.
/// </summary>
/// <param name="Api">The API's version and the commit it was built from.</param>
/// <param name="Content">The last content ingest, or <see langword="null"/> when there was none (or the database is down).</param>
/// <param name="Database">PostgreSQL and the size of the database.</param>
/// <param name="Migrations">Each module's schema: migrations applied and pending.</param>
/// <param name="Dependencies">Each service the API depends on and its state.</param>
/// <param name="Backup">
/// The last good backup. Always <see langword="null"/> until the backup monitoring leaves a heartbeat (#296):
/// absent, not an error.
/// </param>
/// <param name="CheckedAt">When this report was made.</param>
public sealed record SystemReport(
    BuildInfo Api,
    ContentIngestInfo? Content,
    DatabaseInfo Database,
    IReadOnlyList<ModuleMigrations> Migrations,
    IReadOnlyList<DependencyInfo> Dependencies,
    BackupInfo? Backup,
    DateTimeOffset CheckedAt);

/// <param name="Version">The release's version, as release-please set it.</param>
/// <param name="Commit">The commit built, or <see langword="null"/> when the build was not told it.</param>
public sealed record BuildInfo(string Version, string? Commit)
{
    /// <summary>
    /// Splits an informational version (<c>0.1.0+&lt;commit&gt;</c>, as the SDK writes it from
    /// <c>SourceRevisionId</c>) into the version and the commit.
    /// </summary>
    public static BuildInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new BuildInfo("unknown", null);
        }

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return new BuildInfo(informationalVersion.Trim(), null);
        }

        var commit = informationalVersion[(plus + 1)..].Trim();
        return new BuildInfo(informationalVersion[..plus].Trim(), commit.Length == 0 ? null : commit);
    }
}

public sealed record ContentIngestInfo(string Revision, DateTimeOffset IngestedAt);

/// <param name="Status">Whether PostgreSQL answers.</param>
/// <param name="SizeBytes">The database's size, or <see langword="null"/> when PostgreSQL does not answer.</param>
public sealed record DatabaseInfo(DependencyStatus Status, long? SizeBytes);

/// <param name="Module">The module's schema.</param>
/// <param name="Status">Up to date, migrations pending, or unreadable.</param>
/// <param name="Applied">How many migrations are applied, or <see langword="null"/> when unreadable.</param>
/// <param name="Last">The last migration applied, if any.</param>
/// <param name="Pending">The migrations the release carries and the database has not applied.</param>
public sealed record ModuleMigrations(string Module, MigrationStatus Status, int? Applied, string? Last, IReadOnlyList<string> Pending);

public enum MigrationStatus
{
    UpToDate,

    Pending,

    Failing,
}

/// <param name="Name"><c>postgresql</c>, <c>umami</c>, <c>feedbackGitHubApp</c>.</param>
public sealed record DependencyInfo(string Name, DependencyStatus Status);

public enum DependencyStatus
{
    Ok,

    Failing,

    /// <summary>Not set up for this deployment; nothing is wrong.</summary>
    NotConfigured,
}

/// <summary>The last good backup, once #296 leaves a heartbeat to read.</summary>
public sealed record BackupInfo(DateTimeOffset At);
