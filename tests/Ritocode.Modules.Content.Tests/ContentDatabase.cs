using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Persistence;
using Ritocode.TestSupport;

// One PostgreSQL container for the whole assembly, started on first use. The format tests need no
// database and start none, so they still run without Docker.
[assembly: AssemblyFixture(typeof(PostgresTestServer))]

namespace Ritocode.Modules.Content.Tests;

/// <summary>A migrated database of its own for one test class, and contexts over it.</summary>
internal sealed class ContentDatabase
{
    private readonly string _connectionString;

    private ContentDatabase(string connectionString) => _connectionString = connectionString;

    public static async Task<ContentDatabase> CreateAsync(PostgresTestServer postgres, string label)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        return new ContentDatabase(await postgres.CreateDatabaseAsync(label, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A context configured as the host configures one — retries included, because a retrying
    /// strategy changes what code may do with transactions, and a test context without it would
    /// pass code the host then refuses.
    /// </summary>
    public ContentDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(_connectionString, npgsql => npgsql.EnableRetryOnFailure(3))
            .UseSnakeCaseNamingConvention()
            .Options);
}
