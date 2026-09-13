using System.Text;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Files;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Errors;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Workspaces.Tests.Files;

/// <summary>
/// Listing and reading a workspace's files against a real PostgreSQL and a real MinIO. Each workspace
/// is written straight into the table and the bucket, so a snapshot can hold exactly the bytes a test
/// needs — including ones no starter tree would.
/// </summary>
public sealed class WorkspaceFilesTests(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] DefaultEditable = ["src/App.cs", "src/InvoiceSplitter.cs"];

    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly StubWorkspaceAllowanceLookup _allowances = new();

    private CountingObjectStore _store = null!;
    private WorkspacesDatabase _database = null!;

    public async ValueTask InitializeAsync()
    {
        var storage = await minio.CreateBucketsAsync(nameof(WorkspaceFilesTests), TestContext.Current.CancellationToken);
        _store = new CountingObjectStore(minio.CreateStore(storage));
        _database = await WorkspacesDatabase.CreateAsync(postgres, nameof(WorkspaceFilesTests));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task List_ReturnsEveryFile_WithItsSizeInBytes_AndWhetherItMayBeSaved_OrderedByPath()
    {
        var workspace = await WorkspaceHoldingAsync(
            _owner,
            BundleEntry.File("tests/InvoiceSplitterTests.cs", "class T { }"),
            BundleEntry.File("Billing.csproj", "<Project />"),
            BundleEntry.File("src/InvoiceSplitter.cs", "// Übertrag"));

        var result = await ListAsync(_owner, workspace.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                new WorkspaceFileEntry("Billing.csproj", 11, Editable: false),
                new WorkspaceFileEntry("src/InvoiceSplitter.cs", 12, Editable: true),
                new WorkspaceFileEntry("tests/InvoiceSplitterTests.cs", 11, Editable: false),
            ],
            result.Value.Files);
    }

    [Fact]
    public async Task List_OnAVersionThatRecordedNoEditableFiles_MarksNothingEditable()
    {
        // What a version ingested before the allowance was recorded reads as: nothing may be saved.
        var workspace = await WorkspaceAsync(_owner, editable: [], BundleEntry.File("src/App.cs", "app"));

        var result = await ListAsync(_owner, workspace.Id);

        Assert.False(Assert.Single(result.Value.Files).Editable);
    }

    [Fact]
    public async Task Read_ReturnsTheText_ExactlyAsStored_WithTheRevisionOfItsBytes()
    {
        // A byte-order mark, CRLF endings and a non-ASCII character: each is a way for a read to hand
        // an editor text that saves back as different bytes than it was opened from.
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("namespace Billing; // Übertrag\r\n")];
        var workspace = await WorkspaceHoldingAsync(_owner, BundleEntry.Binary("src/InvoiceSplitter.cs", bytes));

        var result = await ReadAsync(_owner, workspace.Id, "src/InvoiceSplitter.cs");

        Assert.True(result.IsSuccess);
        Assert.Equal("src/InvoiceSplitter.cs", result.Value.Path);
        Assert.Equal(bytes.LongLength, result.Value.SizeBytes);
        Assert.Equal("﻿namespace Billing; // Übertrag\r\n", result.Value.Content);
        Assert.Equal(bytes, Encoding.UTF8.GetBytes(result.Value.Content));
        Assert.Equal(FileRevision.Of(bytes), result.Value.Revision);
    }

    [Fact]
    public async Task Read_AnEmptyFile_IsEmptyText()
    {
        var workspace = await WorkspaceHoldingAsync(_owner, BundleEntry.File("src/.gitkeep", string.Empty));

        var result = await ReadAsync(_owner, workspace.Id, "src/.gitkeep");

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value.Content);
        Assert.Equal(0, result.Value.SizeBytes);
    }

    [Theory]
    [InlineData("problem.yaml")]
    [InlineData("src")]
    [InlineData("SRC/App.cs")]
    [InlineData("src/App.cs.bak")]
    public async Task Read_APathTheTreeDoesNotHold_IsFileNotFound(string path)
    {
        // A directory is not a file, and paths compare ordinally: a case-insensitive match would make
        // which of two files a person opened depend on the host's file system.
        var workspace = await WorkspaceHoldingAsync(_owner, BundleEntry.File("src/App.cs", "app"));

        var result = await ReadAsync(_owner, workspace.Id, path);

        AssertFailure(result, ErrorType.NotFound, WorkspaceFiles.FileNotFoundCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../problem.yaml")]
    [InlineData("src/../App.cs")]
    [InlineData("/src/App.cs")]
    [InlineData("src\\App.cs")]
    public async Task Read_APathThatCouldLeaveTheTree_IsRefusedOnPath_BeforeAnythingIsRead(string? path)
    {
        // src/../App.cs names nothing harmful here, and is refused anyway: refusing is the rule, and
        // normalising is the thing ADR 0005 forbids.
        var workspace = await WorkspaceHoldingAsync(_owner, BundleEntry.File("src/App.cs", "app"));
        var gets = _store.Gets;

        var result = await ReadAsync(_owner, workspace.Id, path);

        AssertFailure(result, ErrorType.Validation, "validation_failed");
        Assert.True(result.Error!.Fields?.ContainsKey("path"));
        Assert.Equal(gets, _store.Gets);
    }

    [Fact]
    public async Task Read_BytesThatAreNotUtf8_AreRefusedAsNotText()
    {
        // The first bytes of a PNG. Decoded leniently they would become replacement characters, and
        // an editor saving them back would silently destroy the file.
        var workspace = await WorkspaceHoldingAsync(
            _owner,
            BundleEntry.Binary("assets/logo.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xFF]));

        var result = await ReadAsync(_owner, workspace.Id, "assets/logo.png");

        AssertFailure(result, ErrorType.Conflict, WorkspaceFiles.FileNotTextCode);
    }

    [Fact]
    public async Task BothReads_AnswerAnotherUsersWorkspaceExactlyLikeAMissingOne_AndDownloadNothing()
    {
        var theirs = await WorkspaceHoldingAsync(Guid.CreateVersion7(), BundleEntry.File("src/App.cs", "app"));
        var nobodys = Guid.CreateVersion7();
        var gets = _store.Gets;

        var theirTree = await ListAsync(_owner, theirs.Id);
        var noTree = await ListAsync(_owner, nobodys);
        var theirFile = await ReadAsync(_owner, theirs.Id, "src/App.cs");
        var noFile = await ReadAsync(_owner, nobodys, "src/App.cs");

        AssertFailure(theirTree, ErrorType.NotFound, WorkspaceLifecycle.WorkspaceNotFoundCode);
        AssertFailure(theirFile, ErrorType.NotFound, WorkspaceLifecycle.WorkspaceNotFoundCode);
        Assert.Equal(noTree.Error, theirTree.Error);
        Assert.Equal(noFile.Error, theirFile.Error);
        Assert.Equal(gets, _store.Gets);
    }

    [Fact]
    public async Task AWorkspaceWhoseSnapshotIsMissing_FailsLoudly()
    {
        // Opening writes the snapshot before the row, so this is a broken store — not a 404 that would
        // tell a person their work is gone when the fault is ours.
        var workspace = Workspace.Create(_owner, Guid.CreateVersion7(), Noon);
        await InsertAsync(workspace);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ListAsync(_owner, workspace.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(_owner, workspace.Id, "src/App.cs"));
    }

    [Fact]
    public async Task List_OnAWorkspaceWhoseVersionIsGone_FailsLoudly()
    {
        // Nothing deletes a version, so a workspace outliving one is a broken store as well. Answering
        // every file as read-only instead would look like a problem nobody can solve.
        var workspace = await WorkspaceAsync(_owner, editable: null, BundleEntry.File("src/App.cs", "app"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ListAsync(_owner, workspace.Id));
    }

    private Task<Workspace> WorkspaceHoldingAsync(Guid owner, params BundleEntry[] files) =>
        WorkspaceAsync(owner, DefaultEditable, files);

    /// <param name="editable">The version's editable files, or <see langword="null"/> for a version that does not exist.</param>
    private async Task<Workspace> WorkspaceAsync(Guid owner, string[]? editable, params BundleEntry[] files)
    {
        var workspace = Workspace.Create(owner, Guid.CreateVersion7(), Noon);

        if (editable is not null)
        {
            _allowances.Add(new WorkspaceAllowance(workspace.ProblemVersionId, editable, 50, 65_536, 1_048_576));
        }

        using (var snapshot = Archives.Build(files))
        {
            await _store.PutAsync(workspace.SnapshotReference, snapshot, TestContext.Current.CancellationToken);
        }

        await InsertAsync(workspace);
        return workspace;
    }

    private async Task InsertAsync(Workspace workspace)
    {
        await using var context = _database.CreateContext();
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Result<WorkspaceFileTree>> ListAsync(Guid userId, Guid workspaceId)
    {
        await using var context = _database.CreateContext();

        return await Files(context).ListAsync(userId, workspaceId, TestContext.Current.CancellationToken);
    }

    private async Task<Result<WorkspaceFile>> ReadAsync(Guid userId, Guid workspaceId, string? path)
    {
        await using var context = _database.CreateContext();

        return await Files(context).ReadAsync(userId, workspaceId, path, TestContext.Current.CancellationToken);
    }

    private WorkspaceFiles Files(Persistence.WorkspacesDbContext context) =>
        new(context, _allowances, _store, new FixedClock(Noon));

    private static void AssertFailure<T>(Result<T> result, ErrorType type, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
