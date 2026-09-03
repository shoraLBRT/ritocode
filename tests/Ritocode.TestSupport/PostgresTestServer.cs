using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ritocode.Api.Setup;
using Ritocode.DbMigrator;
using Ritocode.Shared.Modules;
using Testcontainers.PostgreSql;

namespace Ritocode.TestSupport;

/// <summary>
/// One PostgreSQL container for a test assembly, handing out one freshly migrated database per
/// caller. Registered as an xUnit assembly fixture, so the container is started once, on first
/// use, and removed when the assembly finishes.
/// </summary>
/// <remarks>
/// <para>
/// Isolation is per database rather than per transaction: a test that wants to see what the
/// application really wrote — including what a migration, a trigger or a check constraint did —
/// cannot do that inside a transaction the harness later rolls back. Databases are cheap here
/// because each is copied from a template that was migrated once
/// (<c>CREATE DATABASE ... TEMPLATE</c>), so a caller pays a file copy rather than a migration run.
/// </para>
/// <para>
/// Individual databases are not dropped. The container is the lifetime boundary, and it is
/// destroyed when the test assembly ends.
/// </para>
/// </remarks>
public sealed class PostgresTestServer : IAsyncDisposable
{
    /// <summary>Matches <c>compose.yaml</c> and CI, so tests never run on a different major version.</summary>
    public const string Image = "postgres:17-alpine";

    private const string MaintenanceDatabase = "ritocode";
    private const string TemplateDatabase = "ritocode_template";
    private const int MaxIdentifierLength = 63;

    /// <summary>
    /// Serialises container start and database creation. <c>CREATE DATABASE ... TEMPLATE</c> fails
    /// while any session is connected to the template, and concurrent copies of one template
    /// contend in the server; the copy is fast enough that serialising it costs nothing.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    private PostgreSqlContainer? _container;
    private int _databasesCreated;

    /// <summary>
    /// Creates a database of its own, already migrated to the current model, and returns its
    /// connection string. <paramref name="label"/> only makes the database recognisable while a
    /// run is in flight; uniqueness comes from a counter, so the same label may be used twice.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string label, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var container = await StartAsync(cancellationToken);
        var name = DatabaseName(label, Interlocked.Increment(ref _databasesCreated));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ExecuteAsync(container, $"""CREATE DATABASE "{name}" TEMPLATE "{TemplateDatabase}";""", cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return ConnectionStringFor(container, name);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }

        _gate.Dispose();
    }

    private async Task<PostgreSqlContainer> StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                var container = new PostgreSqlBuilder(Image)
                    .WithDatabase(MaintenanceDatabase)
                    .WithUsername("ritocode")
                    .WithPassword("ritocode")
                    // The same ICU locale as compose.yaml and CI. Text ordering differing between
                    // locales would surface as flaky pagination tests rather than as a locale bug.
                    .WithEnvironment("POSTGRES_INITDB_ARGS", "--locale-provider=icu --icu-locale=en-US --encoding=UTF8")
                    .Build();

                await container.StartAsync(cancellationToken);
                await PrepareTemplateAsync(container, cancellationToken);

                _container = container;
            }

            return _container;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task PrepareTemplateAsync(PostgreSqlContainer container, CancellationToken cancellationToken)
    {
        await ExecuteAsync(container, $"""CREATE DATABASE "{TemplateDatabase}";""", cancellationToken);
        await MigrateAsync(ConnectionStringFor(container, TemplateDatabase), cancellationToken);

        // EF returns its connections to the Npgsql pool rather than closing them, and a single
        // pooled connection left open to the template makes every copy of it fail.
        NpgsqlConnection.ClearAllPools();
    }

    private static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                // A retry here would turn a broken migration into a slow failure instead of an
                // immediate, legible one.
                ["Database:MaxRetryCount"] = "0",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddModules(configuration, ModuleRegistry.All);

        await using var provider = services.BuildServiceProvider();
        var runner = new MigrationRunner(provider, provider.GetRequiredService<ILogger<MigrationRunner>>());

        await runner.ApplyAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(PostgreSqlContainer container, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        // CREATE DATABASE cannot run inside a transaction, so there is nothing to wrap this in.
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ConnectionStringFor(PostgreSqlContainer container, string database) =>
        new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = database,
        }.ConnectionString;

    /// <summary>
    /// Builds a legal PostgreSQL identifier: lower case, no punctuation, and short enough that the
    /// server does not silently truncate it — which would turn two databases into one.
    /// </summary>
    private static string DatabaseName(string label, int ordinal)
    {
        const string Prefix = "test_";

        var ordinalText = ordinal.ToString(CultureInfo.InvariantCulture);
        var room = MaxIdentifierLength - Prefix.Length - ordinalText.Length - 1;

        var sanitized = new string([.. label
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_')
            .Take(room)]);

        return $"{Prefix}{sanitized}_{ordinalText}";
    }
}
