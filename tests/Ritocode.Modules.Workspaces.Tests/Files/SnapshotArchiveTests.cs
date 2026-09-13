using Ritocode.Modules.Workspaces.Files;

namespace Ritocode.Modules.Workspaces.Tests.Files;

/// <summary>Reading a workspace snapshot back. No database and no store: the archive in, the tree out.</summary>
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
                new WorkspaceFileEntry("README.md", 0),
                new WorkspaceFileEntry("src/App.cs", 2),
                new WorkspaceFileEntry("tests/AppTests.cs", 5),
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
