using System.Text;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Files;
using Ritocode.Modules.Workspaces.Lifecycle;
using Ritocode.Modules.Workspaces.Persistence;
using Ritocode.Shared.Contracts.Problems;
using Ritocode.Shared.Errors;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Workspaces.Tests.Files;

/// <summary>
/// Saving a workspace file against a real PostgreSQL and a real MinIO: the row lock is PostgreSQL's
/// and the snapshot is really rewritten, so neither is taken on trust. The Problems allowance is
/// answered from a fixed list — what is under test is what this module does with it.
/// </summary>
public sealed class WorkspaceFileWriteTests(PostgresTestServer postgres, MinioTestServer minio) : IAsyncLifetime
{
    private const string AppText = "class App { }\n";
    private const string HelperText = "class Helper { }\n";
    private const string TestsText = "class AppTests { }\n";

    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Noon.AddHours(1);

    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly StubWorkspaceAllowanceLookup _allowances = new();

    private CountingObjectStore _store = null!;
    private WorkspacesDatabase _database = null!;

    public async ValueTask InitializeAsync()
    {
        var storage = await minio.CreateBucketsAsync(nameof(WorkspaceFileWriteTests), TestContext.Current.CancellationToken);
        _store = new CountingObjectStore(minio.CreateStore(storage));
        _database = await WorkspacesDatabase.CreateAsync(postgres, nameof(WorkspaceFileWriteTests));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Write_ReplacesTheFile_AndTheNextReadReturnsIt()
    {
        // The issue's acceptance criteria at the service: the edit persists, and a reload keeps it.
        var workspace = await WorkspaceAsync(_owner);
        const string edited = "class App { int Total; }\n";

        var saved = await WriteAsync(workspace.Id, "src/App.cs", edited, RevisionOf(AppText));

        Assert.True(saved.IsSuccess);
        Assert.Equal("src/App.cs", saved.Value.Path);
        Assert.Equal(Encoding.UTF8.GetByteCount(edited), saved.Value.SizeBytes);
        Assert.Equal(RevisionOf(edited), saved.Value.Revision);

        var reread = await ReadAsync(workspace.Id, "src/App.cs");
        Assert.Equal(edited, reread.Value.Content);
        Assert.Equal(saved.Value.Revision, reread.Value.Revision);

        // The rest of the tree is carried over by the rewrite, not dropped by it.
        Assert.Equal(HelperText, (await ReadAsync(workspace.Id, "src/Helper.cs")).Value.Content);
        Assert.Equal(TestsText, (await ReadAsync(workspace.Id, "tests/AppTests.cs")).Value.Content);
        Assert.Equal(Later, (await RowAsync(workspace.Id)).UpdatedAt);
    }

    [Fact]
    public async Task Write_StoresTheTextExactlyAsSent()
    {
        // A byte-order mark and CRLF endings, sent and read back: no preamble added, none stripped, no
        // line ending rewritten. The contract a read makes, kept by the write.
        var workspace = await WorkspaceAsync(_owner);
        const string text = "﻿namespace App; // Übertrag\r\n";

        var saved = await WriteAsync(workspace.Id, "src/App.cs", text, RevisionOf(AppText));

        var reread = await ReadAsync(workspace.Id, "src/App.cs");
        Assert.Equal(text, reread.Value.Content);
        Assert.Equal(FileRevision.Of([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text[1..])]), saved.Value.Revision);
        Assert.Equal(saved.Value.Revision, reread.Value.Revision);
    }

    [Fact]
    public async Task Write_OverARevisionTheFileHasMovedOnFrom_IsRefused_AndChangesNothing()
    {
        // Two copies of one file: the second is saved over a file the first already replaced. Refused
        // rather than applied, so the first change is not lost without anyone seeing it.
        var workspace = await WorkspaceAsync(_owner);
        Assert.True((await WriteAsync(workspace.Id, "src/App.cs", "class App { int First; }\n", RevisionOf(AppText))).IsSuccess);
        var puts = _store.Puts;

        var second = await WriteAsync(workspace.Id, "src/App.cs", "class App { int Second; }\n", RevisionOf(AppText));

        AssertFailure(second, ErrorType.PreconditionFailed, WorkspaceFiles.FileChangedCode);
        Assert.Equal(puts, _store.Puts);
        Assert.Equal("class App { int First; }\n", (await ReadAsync(workspace.Id, "src/App.cs")).Value.Content);
    }

    [Fact]
    public async Task Write_ToOneFile_IsNotMadeStaleBySavingAnother()
    {
        // The revision is per file. Saving App.cs must not refuse an editor's save of Helper.cs.
        var workspace = await WorkspaceAsync(_owner);
        await WriteAsync(workspace.Id, "src/App.cs", "class App { int First; }\n", RevisionOf(AppText));

        var helper = await WriteAsync(workspace.Id, "src/Helper.cs", "class Helper { int Second; }\n", RevisionOf(HelperText));

        Assert.True(helper.IsSuccess);
        Assert.Equal("class App { int First; }\n", (await ReadAsync(workspace.Id, "src/App.cs")).Value.Content);
    }

    [Fact]
    public async Task ConcurrentWritesToDifferentFiles_BothSurvive()
    {
        // Each save rewrites the whole snapshot. Without the row lock both would read the tree before
        // either wrote, and the later put would drop the earlier save. Several rounds, so an
        // implementation that is only sometimes serialised does not pass by luck.
        for (var round = 0; round < 5; round++)
        {
            var workspace = await WorkspaceAsync(_owner);
            var app = $"class App {{ int Round{round}; }}\n";
            var helper = $"class Helper {{ int Round{round}; }}\n";

            var results = await Task.WhenAll(
                WriteAsync(workspace.Id, "src/App.cs", app, RevisionOf(AppText)),
                WriteAsync(workspace.Id, "src/Helper.cs", helper, RevisionOf(HelperText)));

            Assert.All(results, result => Assert.True(result.IsSuccess));
            Assert.Equal(app, (await ReadAsync(workspace.Id, "src/App.cs")).Value.Content);
            Assert.Equal(helper, (await ReadAsync(workspace.Id, "src/Helper.cs")).Value.Content);
        }
    }

    [Fact]
    public async Task ConcurrentWritesToOneFileFromOneRevision_SaveOne_AndRefuseTheOther()
    {
        // The lock and the revision together: the second writer waits, then finds the file moved.
        var workspace = await WorkspaceAsync(_owner);

        var results = await Task.WhenAll(
            WriteAsync(workspace.Id, "src/App.cs", "class App { int A; }\n", RevisionOf(AppText)),
            WriteAsync(workspace.Id, "src/App.cs", "class App { int B; }\n", RevisionOf(AppText)));

        Assert.NotEqual(results[0].IsSuccess, results[1].IsSuccess);

        var saved = results.First(result => result.IsSuccess);
        var refused = results.First(result => !result.IsSuccess);

        Assert.Equal(WorkspaceFiles.FileChangedCode, refused.Error!.Code);
        Assert.Equal(saved.Value!.Revision, (await ReadAsync(workspace.Id, "src/App.cs")).Value.Revision);
    }

    [Fact]
    public async Task Write_AReadOnlyFile_IsForbidden_AndChangesNothing()
    {
        // The tests decide the verdict. A save that could change them would let a submission grade
        // itself — and the orchestrator restoring them later (#17) is the second guard, not the first.
        var workspace = await WorkspaceAsync(_owner);
        var puts = _store.Puts;

        var result = await WriteAsync(workspace.Id, "tests/AppTests.cs", "class AppTests { }\n// pass\n", RevisionOf(TestsText));

        AssertFailure(result, ErrorType.Forbidden, WorkspaceFiles.FileReadOnlyCode);
        Assert.Equal(puts, _store.Puts);
        Assert.Equal(TestsText, (await ReadAsync(workspace.Id, "tests/AppTests.cs")).Value.Content);
    }

    [Fact]
    public async Task Write_OnAVersionThatRecordedNoEditableFiles_RefusesEveryFile()
    {
        // What a version ingested before the allowance was recorded reads as.
        var workspace = await WorkspaceAsync(_owner, editable: []);

        var result = await WriteAsync(workspace.Id, "src/App.cs", "class App { int Total; }\n", RevisionOf(AppText));

        AssertFailure(result, ErrorType.Forbidden, WorkspaceFiles.FileReadOnlyCode);
    }

    [Theory]
    [InlineData("src/New.cs")]
    [InlineData("SRC/App.cs")]
    [InlineData("src")]
    public async Task Write_APathTheTreeDoesNotHold_IsFileNotFound_AndCreatesNothing(string path)
    {
        // A save replaces; it never adds. Creating a file would need the version's globs, which are the
        // Problems module's format — see docs/PROJECT_STATE.md.
        var workspace = await WorkspaceAsync(_owner);
        var puts = _store.Puts;

        var result = await WriteAsync(workspace.Id, path, "class New { }\n", RevisionOf(AppText));

        AssertFailure(result, ErrorType.NotFound, WorkspaceFiles.FileNotFoundCode);
        Assert.Equal(puts, _store.Puts);
        Assert.Equal(
            ["src/App.cs", "src/Helper.cs", "tests/AppTests.cs"],
            (await ListAsync(workspace.Id)).Value.Files.Select(file => file.Path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../problem.yaml")]
    [InlineData("src/../src/App.cs")]
    [InlineData("/src/App.cs")]
    [InlineData("src\\App.cs")]
    public async Task Write_APathThatCouldLeaveTheTree_IsRefusedOnPath_BeforeAnythingIsRead(string path)
    {
        // ADR 0005's forbidden row about writes, at the service: refused by name, never normalised.
        var workspace = await WorkspaceAsync(_owner);
        var (gets, puts) = (_store.Gets, _store.Puts);

        var result = await WriteAsync(workspace.Id, path, "class App { }\n", RevisionOf(AppText));

        AssertFailure(result, ErrorType.Validation, "validation_failed");
        Assert.True(result.Error!.Fields?.ContainsKey("path"));
        Assert.Equal((gets, puts), (_store.Gets, _store.Puts));
    }

    [Fact]
    public async Task Write_OverTheVersionsPerFileLimit_IsRefusedOnContent()
    {
        // 33 bytes from 17 characters: the limit is on what is stored, not on what an editor counts.
        var workspace = await WorkspaceAsync(_owner, maxFileBytes: 32, maxTotalBytes: 1_024);
        var puts = _store.Puts;

        var result = await WriteAsync(workspace.Id, "src/App.cs", new string('ü', 16) + "a", RevisionOf(AppText));

        AssertFailure(result, ErrorType.Validation, "validation_failed");
        Assert.True(result.Error!.Fields?.ContainsKey("content"));
        Assert.Equal(puts, _store.Puts);
    }

    [Fact]
    public async Task Write_ExactlyAtThePerFileLimit_IsSaved()
    {
        var workspace = await WorkspaceAsync(_owner, maxFileBytes: 32, maxTotalBytes: 1_024);

        var result = await WriteAsync(workspace.Id, "src/App.cs", new string('a', 32), RevisionOf(AppText));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Write_ThatWouldOutgrowTheTree_IsALimitConflict_AndChangesNothing()
    {
        // Within the per-file limit, over the tree's: 30 bytes beside Helper's 17 and the tests' 19.
        var workspace = await WorkspaceAsync(_owner, maxFileBytes: 32, maxTotalBytes: 60);
        var puts = _store.Puts;

        var result = await WriteAsync(workspace.Id, "src/App.cs", new string('a', 30), RevisionOf(AppText));

        AssertFailure(result, ErrorType.Conflict, WorkspaceFiles.LimitExceededCode);
        Assert.Equal(puts, _store.Puts);
        Assert.Equal(AppText, (await ReadAsync(workspace.Id, "src/App.cs")).Value.Content);
    }

    [Fact]
    public async Task Write_TextWithNoUtf8Form_IsRefusedOnContent_BeforeAnythingIsRead()
    {
        // A lone surrogate. Encoded leniently it would be stored as a replacement character — a file
        // that no longer says what was sent.
        var workspace = await WorkspaceAsync(_owner);
        var gets = _store.Gets;

        var result = await WriteAsync(workspace.Id, "src/App.cs", "class App { \uD800 }\n", RevisionOf(AppText));

        AssertFailure(result, ErrorType.Validation, "validation_failed");
        Assert.True(result.Error!.Fields?.ContainsKey("content"));
        Assert.Equal(gets, _store.Gets);
    }

    [Fact]
    public async Task Write_WhatIsAlreadyStored_WritesNothing_AndLeavesUpdatedAtAlone()
    {
        var workspace = await WorkspaceAsync(_owner);
        var puts = _store.Puts;

        var result = await WriteAsync(workspace.Id, "src/App.cs", AppText, RevisionOf(AppText));

        Assert.True(result.IsSuccess);
        Assert.Equal(RevisionOf(AppText), result.Value.Revision);
        Assert.Equal(puts, _store.Puts);
        Assert.Equal(Noon, (await RowAsync(workspace.Id)).UpdatedAt);
    }

    [Fact]
    public async Task Write_AnswersAnotherUsersWorkspaceExactlyLikeAMissingOne_AndReadsNothing()
    {
        var theirs = await WorkspaceAsync(Guid.CreateVersion7());
        var (gets, puts) = (_store.Gets, _store.Puts);

        var theirResult = await WriteAsync(theirs.Id, "src/App.cs", "class Mine { }\n", RevisionOf(AppText));
        var noResult = await WriteAsync(Guid.CreateVersion7(), "src/App.cs", "class Mine { }\n", RevisionOf(AppText));

        AssertFailure(theirResult, ErrorType.NotFound, WorkspaceLifecycle.WorkspaceNotFoundCode);
        Assert.Equal(noResult.Error, theirResult.Error);
        Assert.Equal((gets, puts), (_store.Gets, _store.Puts));
        Assert.Equal(Noon, (await RowAsync(theirs.Id)).UpdatedAt);
    }

    /// <summary>
    /// A workspace holding App.cs, Helper.cs and AppTests.cs, on a version of its own whose editable
    /// files are the first two unless the test says otherwise.
    /// </summary>
    private async Task<Workspace> WorkspaceAsync(
        Guid owner,
        string[]? editable = null,
        int maxFileBytes = 65_536,
        int maxTotalBytes = 1_048_576)
    {
        var workspace = Workspace.Create(owner, Guid.CreateVersion7(), Noon);

        _allowances.Add(new WorkspaceAllowance(
            workspace.ProblemVersionId,
            editable ?? ["src/App.cs", "src/Helper.cs"],
            MaxFiles: 50,
            maxFileBytes,
            maxTotalBytes));

        using (var snapshot = Archives.Build(
            BundleEntry.File("src/App.cs", AppText),
            BundleEntry.File("src/Helper.cs", HelperText),
            BundleEntry.File("tests/AppTests.cs", TestsText)))
        {
            await _store.PutAsync(workspace.SnapshotReference, snapshot, TestContext.Current.CancellationToken);
        }

        await using var context = _database.CreateContext();
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return workspace;
    }

    private async Task<Result<SavedWorkspaceFile>> WriteAsync(Guid workspaceId, string? path, string content, string baseRevision)
    {
        await using var context = _database.CreateContext();

        return await Files(context).WriteAsync(_owner, workspaceId, path, content, baseRevision, TestContext.Current.CancellationToken);
    }

    private async Task<Result<WorkspaceFile>> ReadAsync(Guid workspaceId, string path)
    {
        await using var context = _database.CreateContext();

        var result = await Files(context).ReadAsync(_owner, workspaceId, path, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);

        return result;
    }

    private async Task<Result<WorkspaceFileTree>> ListAsync(Guid workspaceId)
    {
        await using var context = _database.CreateContext();

        return await Files(context).ListAsync(_owner, workspaceId, TestContext.Current.CancellationToken);
    }

    private async Task<Workspace> RowAsync(Guid workspaceId)
    {
        await using var context = _database.CreateContext();

        return await context.Workspaces
            .AsNoTracking()
            .SingleAsync(workspace => workspace.Id == workspaceId, TestContext.Current.CancellationToken);
    }

    private WorkspaceFiles Files(WorkspacesDbContext context) => new(context, _allowances, _store, new FixedClock(Later));

    private static string RevisionOf(string text) => FileRevision.Of(Encoding.UTF8.GetBytes(text));

    private static void AssertFailure<T>(Result<T> result, ErrorType type, string code)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
