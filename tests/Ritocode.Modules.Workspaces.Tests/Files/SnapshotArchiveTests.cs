using Ritocode.Modules.Workspaces.Files;

namespace Ritocode.Modules.Workspaces.Tests.Files;

/// <summary>
/// Reading a workspace snapshot back, and rewriting one file of it. No database and no store: the
/// archive in, the tree out.
/// </summary>
public sealed class SnapshotArchiveTests
{
    [Fact]
    public async Task List_ReturnsEveryFile_WithItsLengthInBytes_OrderedByPath()
    {
        // Written out of order, with a directory entry and a two-byte character: the order a client
        // sees is not the archive's, a directory is not a file, and a size is not a character count.
        using var snapshot = Archives.Build(
            BundleEntry.File("tests/AppTests.cs", "tests"),
            BundleEntry.Directory("src/"),
            BundleEntry.File("src/App.cs", "é"),
            BundleEntry.File("README.md", string.Empty));

        var files = await SnapshotArchive.ListAsync(snapshot, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new SnapshotFile("README.md", 0),
                new SnapshotFile("src/App.cs", 2),
                new SnapshotFile("tests/AppTests.cs", 5),
            ],
            files);
    }

    [Fact]
    public async Task Read_ReturnsTheBytesOfThatFile_AndNullForAPathTheSnapshotDoesNotHold()
    {
        using var snapshot = Archives.Build(
            BundleEntry.File("a.txt", "first"),
            BundleEntry.File("b.txt", "second"));

        Assert.Equal("second"u8.ToArray(), await SnapshotArchive.ReadAsync(snapshot, "b.txt", TestContext.Current.CancellationToken));

        snapshot.Position = 0;
        Assert.Null(await SnapshotArchive.ReadAsync(snapshot, "c.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_AnEmptyFile_IsNoBytes_RatherThanMissing()
    {
        using var snapshot = Archives.Build(BundleEntry.File("src/.gitkeep", string.Empty));

        var bytes = await SnapshotArchive.ReadAsync(snapshot, "src/.gitkeep", TestContext.Current.CancellationToken);

        Assert.NotNull(bytes);
        Assert.Empty(bytes);
    }

    [Fact]
    public async Task Replace_SwapsTheBytesOfOneFile_AndCopiesEveryOtherUnchanged()
    {
        using var snapshot = Archives.Build(
            BundleEntry.File("a.txt", "first"),
            BundleEntry.File("b.txt", "second"),
            BundleEntry.File("c.txt", "third"));
        using var destination = new MemoryStream();

        var replacement = await SnapshotArchive.ReplaceAsync(
            snapshot, "b.txt", "changed!"u8.ToArray(), destination, TestContext.Current.CancellationToken);

        Assert.Equal("second"u8.ToArray(), replacement.Previous);
        Assert.Equal(3, replacement.FileCount);
        Assert.Equal(18L, replacement.TotalBytes);
        Assert.Equal<(string, string)>(
            [("a.txt", "first"), ("b.txt", "changed!"), ("c.txt", "third")],
            (await Archives.ReadAsync(destination)).Select(entry => (entry.Path, entry.Content)));
    }

    [Fact]
    public async Task Replace_APathTheSnapshotDoesNotHold_ReplacesNothing_AndAddsNothing()
    {
        // A save replaces a file; it never creates one. The copy is there to be discarded.
        using var snapshot = Archives.Build(BundleEntry.File("a.txt", "first"));
        using var destination = new MemoryStream();

        var replacement = await SnapshotArchive.ReplaceAsync(
            snapshot, "new.txt", "new"u8.ToArray(), destination, TestContext.Current.CancellationToken);

        Assert.Null(replacement.Previous);
        Assert.Equal(["a.txt"], (await Archives.ReadAsync(destination)).Select(entry => entry.Path));
    }

    [Fact]
    public async Task Replace_OnACorruptSnapshot_Throws_RatherThanSavingItBackClean()
    {
        // The rewrite reads through the same checks as a list: a link that was skipped here would be
        // gone from the saved tree, and the evidence that the snapshot was tampered with gone with it.
        using var snapshot = Archives.Build(
            BundleEntry.File("src/App.cs", "app"),
            BundleEntry.SymbolicLink("src/secrets", "/etc/passwd"));
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() => SnapshotArchive.ReplaceAsync(
            snapshot, "src/App.cs", "changed"u8.ToArray(), destination, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ALink_IsACorruptSnapshot()
    {
        using var snapshot = Archives.Build(
            BundleEntry.File("src/App.cs", "app"),
            BundleEntry.SymbolicLink("src/secrets", "/etc/passwd"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SnapshotArchive.ListAsync(snapshot, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../escape.cs")]
    [InlineData("/absolute.cs")]
    [InlineData("src//App.cs")]
    public async Task AnEntryThatIsNotAWorkspacePath_IsACorruptSnapshot(string name)
    {
        using var snapshot = Archives.Build(BundleEntry.File(name, "content"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SnapshotArchive.ListAsync(snapshot, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSamePathTwice_IsACorruptSnapshot()
    {
        using var snapshot = Archives.Build(
            BundleEntry.File("src/App.cs", "first"),
            BundleEntry.File("src/App.cs", "second"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            SnapshotArchive.ListAsync(snapshot, TestContext.Current.CancellationToken));
    }
}
