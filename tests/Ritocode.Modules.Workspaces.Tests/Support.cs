using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Storage;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Workspaces.Tests;

/// <summary>
/// A migrated database, and contexts over it configured the way <c>AddModuleDbContext</c> configures
/// the host's — including the naming convention, without which every query names columns that do not
/// exist.
/// </summary>
internal sealed class WorkspacesDatabase
{
    private readonly string _connectionString;

    private WorkspacesDatabase(string connectionString) => _connectionString = connectionString;

    public static async Task<WorkspacesDatabase> CreateAsync(PostgresTestServer postgres, string label) =>
        new(await postgres.CreateDatabaseAsync(label, TestContext.Current.CancellationToken));

    /// <summary>A new context each call, so an assertion reads the database rather than a change tracker.</summary>
    public WorkspacesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}

/// <summary>One entry of a hand-built bundle or snapshot.</summary>
internal sealed record BundleEntry(
    string Name,
    TarEntryType Type,
    string? Content = null,
    string? LinkName = null,
    byte[]? Bytes = null)
{
    public static BundleEntry File(string name, string content) => new(name, TarEntryType.RegularFile, content);

    /// <summary>A file whose bytes are given exactly, for content that is not — or not only — text.</summary>
    public static BundleEntry Binary(string name, byte[] bytes) => new(name, TarEntryType.RegularFile, Bytes: bytes);

    public static BundleEntry Directory(string name) => new(name, TarEntryType.Directory);

    public static BundleEntry SymbolicLink(string name, string target) =>
        new(name, TarEntryType.SymbolicLink, LinkName: target);
}

/// <summary>Gzipped tars built and read in memory — the object formats of docs/STORAGE_LAYOUT.md.</summary>
internal static class Archives
{
    /// <summary>The starter files of <see cref="TypicalBundle"/>, as a workspace should hold them.</summary>
    public static readonly string[] TypicalStarterPaths =
    [
        "Billing.csproj",
        "src/InvoiceSplitter.cs",
        "tests/InvoiceSplitterTests.cs",
    ];

    /// <summary>
    /// What ingest writes for a package rooted at <c>starter</c>: manifest and description beside the
    /// starter tree, in ordinal order. Built by hand rather than by the Problems module's writer,
    /// because this module's tests may depend on the bundle's shape and not on that module's code.
    /// </summary>
    public static MemoryStream TypicalBundle() => Build(
        BundleEntry.File("description.md", "# Split the invoice"),
        BundleEntry.File("problem.yaml", "schema_version: 1"),
        BundleEntry.File("starter/Billing.csproj", "<Project />"),
        BundleEntry.File("starter/src/InvoiceSplitter.cs", "class InvoiceSplitter { }"),
        BundleEntry.File("starter/tests/InvoiceSplitterTests.cs", "class InvoiceSplitterTests { }"));

    public static MemoryStream Build(params BundleEntry[] entries)
    {
        var archive = new MemoryStream();

        using (var gzip = new GZipStream(archive, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var tarEntry = new PaxTarEntry(entry.Type, entry.Name);

                var data = entry.Bytes ?? (entry.Content is null ? null : Encoding.UTF8.GetBytes(entry.Content));

                if (data is not null)
                {
                    tarEntry.DataStream = new MemoryStream(data);
                }

                if (entry.LinkName is not null)
                {
                    tarEntry.LinkName = entry.LinkName;
                }

                writer.WriteEntry(tarEntry);
            }
        }

        archive.Position = 0;
        return archive;
    }

    /// <summary>Every entry of an archive, in archive order, with its type and its text.</summary>
    public static async Task<List<(string Path, TarEntryType Type, string Content)>> ReadAsync(Stream archive)
    {
        archive.Position = 0;

        await using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
        await using var reader = new TarReader(gzip, leaveOpen: true);

        var entries = new List<(string, TarEntryType, string)>();

        while (await reader.GetNextEntryAsync(cancellationToken: TestContext.Current.CancellationToken) is { } entry)
        {
            var content = string.Empty;

            if (entry.DataStream is not null)
            {
                using var text = new StreamReader(entry.DataStream, Encoding.UTF8);
                content = await text.ReadToEndAsync(TestContext.Current.CancellationToken);
            }

            entries.Add((entry.Name, entry.EntryType, content));
        }

        return entries;
    }
}

/// <summary>
/// The Users contract answered from a fixed list. The real implementation is tested against a real
/// database in its own module, and resolved from the composed host in Ritocode.Api.Tests; here the
/// question is what Workspaces does with each answer.
/// </summary>
internal sealed class StubUserLookup(params UserSummary[] users) : IUserLookup
{
    public Task<UserSummary?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(users.FirstOrDefault(user => user.Id == id));
}

/// <summary>The Problems contract answered from a fixed list. See <see cref="StubUserLookup"/>.</summary>
internal sealed class StubProblemVersionLookup(params ProblemVersionSummary[] versions) : IProblemVersionLookup
{
    public Task<ProblemVersionSummary?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(versions.FirstOrDefault(version => version.Id == id));
}

/// <summary>
/// A real store that also counts puts and gets, so a test can assert that nothing was written — or
/// that nothing was read.
/// </summary>
internal sealed class CountingObjectStore(IObjectStore inner) : IObjectStore
{
    public int Puts { get; private set; }

    public int Gets { get; private set; }

    public Task PutAsync(StorageReference reference, Stream content, CancellationToken cancellationToken = default)
    {
        Puts++;
        return inner.PutAsync(reference, content, cancellationToken);
    }

    public Task<bool> GetAsync(StorageReference reference, Stream destination, CancellationToken cancellationToken = default)
    {
        Gets++;
        return inner.GetAsync(reference, destination, cancellationToken);
    }
}

/// <summary>A clock that does not move, so a stored timestamp is an assertion and not a range.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
