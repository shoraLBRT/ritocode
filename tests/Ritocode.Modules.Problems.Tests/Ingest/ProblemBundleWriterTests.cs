using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Ritocode.Modules.Problems.Ingest;
using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests.Ingest;

/// <summary>
/// What the bundle carries, checked against the reference package as it is committed. The rule
/// under test is the one docs/STORAGE_LAYOUT.md states: the bundle is the manifest, the description
/// and the workspace root, and nothing else — which is what lets it be served without filtering.
/// </summary>
public sealed class ProblemBundleWriterTests
{
    [Fact]
    public async Task Bundle_HoldsTheManifestTheDescriptionAndTheWorkspaceRoot()
    {
        var package = LoadExample();

        var entries = await ReadEntryNamesAsync(package);

        Assert.Equal(
            [
                "description.md",
                "problem.yaml",
                "starter/Orders.csproj",
                "starter/README.md",
                "starter/src/OrderLine.cs",
                "starter/src/OrderTotal.cs",
                "starter/tests/OrderTotalTests.cs",
            ],
            entries);
    }

    [Fact]
    public async Task Bundle_LeavesTheFixturesOut()
    {
        var package = LoadExample();

        // The package really does have fixtures, or the assertion below would pass for the wrong
        // reason.
        Assert.NotNull(package.Manifest.Fixtures.Passing);
        Assert.NotNull(package.Manifest.Fixtures.Failing);

        var entries = await ReadEntryNamesAsync(package);

        Assert.DoesNotContain(entries, entry => entry.StartsWith("fixtures/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bundle_CarriesTheFileContentsUnchanged()
    {
        var package = LoadExample();
        const string Entry = "starter/src/OrderTotal.cs";

        var extracted = await ReadEntryTextAsync(package, Entry);

        Assert.Equal(
            await File.ReadAllTextAsync(
                Path.Combine(package.PackageDirectory, "starter", "src", "OrderTotal.cs"),
                TestContext.Current.CancellationToken),
            extracted);
    }

    [Fact]
    public async Task Bundle_OrdersItsEntriesTheSameWayEveryTime()
    {
        var package = LoadExample();

        Assert.Equal(await ReadEntryNamesAsync(package), await ReadEntryNamesAsync(package));
    }

    private static ProblemPackage LoadExample()
    {
        var loaded = ProblemPackageLoader.Load(ExamplePackage.Directory);

        Assert.True(loaded.IsSuccess, loaded.IsSuccess ? string.Empty : loaded.Error.Message);

        return loaded.Value;
    }

    private static async Task<IReadOnlyList<string>> ReadEntryNamesAsync(ProblemPackage package)
    {
        var names = new List<string>();

        await ReadAsync(package, async entry =>
        {
            names.Add(entry.Name);
            await Task.CompletedTask;
        });

        return names;
    }

    private static async Task<string?> ReadEntryTextAsync(ProblemPackage package, string entryName)
    {
        string? text = null;

        await ReadAsync(package, async entry =>
        {
            if (!string.Equals(entry.Name, entryName, StringComparison.Ordinal) || entry.DataStream is null)
            {
                return;
            }

            using var reader = new StreamReader(entry.DataStream, Encoding.UTF8, leaveOpen: true);
            text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        });

        return text;
    }

    private static async Task ReadAsync(ProblemPackage package, Func<TarEntry, Task> onEntry)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var archive = new MemoryStream();
        await ProblemBundleWriter.WriteAsync(package, archive, cancellationToken);
        archive.Position = 0;

        await using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        await using var reader = new TarReader(gzip);

        while (await reader.GetNextEntryAsync(cancellationToken: cancellationToken) is { } entry)
        {
            await onEntry(entry);
        }
    }
}
