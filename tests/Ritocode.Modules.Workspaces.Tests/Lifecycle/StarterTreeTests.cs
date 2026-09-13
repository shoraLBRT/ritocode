using System.Formats.Tar;
using Ritocode.Modules.Workspaces.Lifecycle;

namespace Ritocode.Modules.Workspaces.Tests.Lifecycle;

/// <summary>
/// What a problem bundle becomes when a workspace is materialised from it. No database and no store:
/// the archive in, the archive out.
/// </summary>
public sealed class StarterTreeTests
{
    [Fact]
    public async Task CopiesTheFilesUnderTheRoot_AtThePathsAUserSees()
    {
        using var bundle = Archives.TypicalBundle();
        using var snapshot = new MemoryStream();

        var written = await StarterTree.WriteAsync(bundle, "starter", snapshot, TestContext.Current.CancellationToken);

        // The manifest and the description are beside the root, not in it, so neither reaches a
        // workspace — and no path keeps the root directory, which means nothing to the user.
        Assert.Equal(Archives.TypicalStarterPaths, written);

        var entries = await Archives.ReadAsync(snapshot);
        Assert.Equal(Archives.TypicalStarterPaths, entries.Select(entry => entry.Path));
        Assert.All(entries, entry => Assert.Equal(TarEntryType.RegularFile, entry.Type));
        Assert.Equal("class InvoiceSplitter { }", entries.Single(entry => entry.Path == "src/InvoiceSplitter.cs").Content);
    }

    [Fact]
    public async Task ASiblingWhoseNameMerelyStartsWithTheRoot_IsNotPartOfTheTree()
    {
        using var bundle = Archives.Build(
            BundleEntry.File("starter-old/src/Legacy.cs", "old"),
            BundleEntry.File("starter.md", "notes"),
            BundleEntry.File("starter/src/Current.cs", "current"));
        using var snapshot = new MemoryStream();

        var written = await StarterTree.WriteAsync(bundle, "starter", snapshot, TestContext.Current.CancellationToken);

        Assert.Equal(["src/Current.cs"], written);
    }

    [Fact]
    public async Task ANestedRoot_IsResolvedAsAWholePath()
    {
        using var bundle = Archives.Build(
            BundleEntry.File("tasks/one/Program.cs", "one"),
            BundleEntry.File("tasks/two/Program.cs", "two"));
        using var snapshot = new MemoryStream();

        var written = await StarterTree.WriteAsync(bundle, "tasks/two", snapshot, TestContext.Current.CancellationToken);

        Assert.Equal(["Program.cs"], written);
        Assert.Equal("two", (await Archives.ReadAsync(snapshot)).Single().Content);
    }

    [Fact]
    public async Task DirectoryEntriesAreSkipped_AndAnEmptyFileSurvives()
    {
        using var bundle = Archives.Build(
            BundleEntry.Directory("starter/"),
            BundleEntry.Directory("starter/src/"),
            BundleEntry.File("starter/src/.gitkeep", string.Empty),
            BundleEntry.File("starter/src/App.cs", "app"));
        using var snapshot = new MemoryStream();

        var written = await StarterTree.WriteAsync(bundle, "starter", snapshot, TestContext.Current.CancellationToken);

        Assert.Equal(["src/.gitkeep", "src/App.cs"], written);
        Assert.Equal(string.Empty, (await Archives.ReadAsync(snapshot))[0].Content);
    }

    [Fact]
    public async Task ALinkUnderTheRoot_IsRefused()
    {
        // A link in a workspace is a path that resolves outside it the moment anything follows it.
        using var bundle = Archives.Build(
            BundleEntry.File("starter/src/App.cs", "app"),
            BundleEntry.SymbolicLink("starter/src/secrets", "/etc/passwd"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StarterTree.WriteAsync(bundle, "starter", new MemoryStream(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("starter/../problem.yaml")]
    [InlineData("starter/./src/App.cs")]
    [InlineData("starter//src/App.cs")]
    public async Task APathThatCouldLeaveTheTree_IsRefusedRatherThanNormalised(string name)
    {
        using var bundle = Archives.Build(BundleEntry.File(name, "content"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StarterTree.WriteAsync(bundle, "starter", new MemoryStream(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSamePathTwice_IsRefused()
    {
        // Tar allows it and the later entry silently wins on extraction — which of two trees a user
        // opened would depend on who unpacked it.
        using var bundle = Archives.Build(
            BundleEntry.File("starter/src/App.cs", "first"),
            BundleEntry.File("starter/src/App.cs", "second"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StarterTree.WriteAsync(bundle, "starter", new MemoryStream(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ABundleWithNothingUnderTheRoot_IsRefused()
    {
        // A root that names no directory of this bundle means the row and the object disagree. An
        // empty workspace would hide that until someone wondered why their editor was blank.
        using var bundle = Archives.TypicalBundle();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StarterTree.WriteAsync(bundle, "workspace", new MemoryStream(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/starter")]
    [InlineData("../starter")]
    [InlineData("starter/")]
    [InlineData("starter\\src")]
    public async Task ARootThatIsNotABundleRelativeDirectory_IsAnArgumentError(string root)
    {
        using var bundle = Archives.TypicalBundle();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            StarterTree.WriteAsync(bundle, root, new MemoryStream(), TestContext.Current.CancellationToken));
    }
}
