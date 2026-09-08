using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// A migrated database, and contexts over it configured exactly as <c>AddModuleDbContext</c>
/// configures the host's.
/// </summary>
/// <remarks>
/// <para>
/// One per test rather than one per class. Ingest counts versions per slug, so two tests that both
/// ingest the reference package would otherwise depend on which of them ran first — the kind of
/// coupling that shows up as a failure in whichever test was later renamed.
/// </para>
/// <para>
/// The naming convention is not a detail to leave out: the host maps <c>SnapshotReference</c> onto
/// <c>snapshot_reference</c>, and a context built without it would query columns that do not exist
/// while every assertion in the test still looked right.
/// </para>
/// </remarks>
internal sealed class ProblemsDatabase
{
    private readonly string _connectionString;

    private ProblemsDatabase(string connectionString) => _connectionString = connectionString;

    public static async Task<ProblemsDatabase> CreateAsync(PostgresTestServer postgres, string label)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        return new ProblemsDatabase(
            await postgres.CreateDatabaseAsync(label, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A new context each call. A test that writes and then reads uses separate ones, so the
    /// assertion reads what the database holds rather than what the change tracker remembers.
    /// </summary>
    public ProblemsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ProblemsDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}
