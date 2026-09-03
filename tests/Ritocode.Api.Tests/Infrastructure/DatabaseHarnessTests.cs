using Npgsql;
using Ritocode.Shared.Persistence;
using Ritocode.TestSupport;

namespace Ritocode.Api.Tests.Infrastructure;

/// <summary>
/// Tests for the harness itself. Every module test written from here on trusts that a database
/// handed out by <see cref="PostgresTestServer"/> is migrated and is nobody else's, so both claims
/// are asserted rather than assumed.
/// </summary>
public sealed class DatabaseHarnessTests(PostgresTestServer postgres)
{
    [Fact]
    public async Task NewDatabase_HasEveryModuleSchemaMigrated()
    {
        var connectionString = await postgres.CreateDatabaseAsync(
            nameof(NewDatabase_HasEveryModuleSchemaMigrated),
            TestContext.Current.CancellationToken);

        var migrated = await QueryStringsAsync(
            connectionString,
            $"""
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_name = '{ModuleDbContextExtensions.MigrationsHistoryTableName}'
            """);

        Assert.Equal(
            ModuleSchemas.All.Order(StringComparer.Ordinal),
            migrated.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SeparateDatabases_DoNotSeeEachOthersWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = await postgres.CreateDatabaseAsync("isolation_first", cancellationToken);
        var second = await postgres.CreateDatabaseAsync("isolation_second", cancellationToken);

        Assert.NotEqual(first, second);

        await ExecuteAsync(first, "CREATE TABLE public.only_in_the_first (id int primary key)");

        var tablesInSecond = await QueryStringsAsync(
            second,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        Assert.DoesNotContain("only_in_the_first", tablesInSecond, StringComparer.Ordinal);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<string>> QueryStringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var values = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
