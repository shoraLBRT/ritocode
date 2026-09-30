using Ritocode.Modules.Content.Contracts;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Tests.Format;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Content.Tests.Contracts;

/// <summary>What the Attempts module learns about a task, over ingested reference content.</summary>
public sealed class TaskForAttemptLookupTests(PostgresTestServer postgres) : IAsyncLifetime
{
    private ContentDatabase _database = null!;
    private TempContent _content = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await ContentDatabase.CreateAsync(postgres, nameof(TaskForAttemptLookupTests));

        _content = TempContent.FromReference()
            .Write("tasks/invoice-mailer-hosted/task.yaml", "material: invoice-mailer\ndifficulty: medium\nfindings:\n  - card: hardcoded-config\n    leaves: [manual.extract-config]\n")
            .Write("tasks/invoice-mailer-hosted/ru.md", "---\ntitle: Счета для сотни студий\n---\n\n## Context\n\nСервис.\n\n## Brief\n\n«То же.»\n");

        await IngestAsync("first");
    }

    public ValueTask DisposeAsync()
    {
        _content.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ATask_ComesWithItsKey_TheWeights_TheOfferedCards_AndTheLeaves()
    {
        var task = await FindAsync("invoice-mailer-monthly");

        Assert.NotNull(task);
        Assert.True(task.Published);
        Assert.Equal("first", task.ContentRevision);
        Assert.Equal(
            [("secrets-in-repo", 3, "auto.secrets,rule.conventions"), ("money-in-float", 3, "manual.representation"), ("swallowed-error", 2, "manual.handle-errors")],
            task.Findings.Select(finding => (finding.Card, finding.Weight, string.Join(',', finding.Leaves))));

        // An easy task offers its shortlist, which over four cards is all of them.
        Assert.Equal(["hardcoded-config", "money-in-float", "secrets-in-repo", "swallowed-error"], task.OfferedCards);
        Assert.Equal(29, task.Leaves.Count);

        // The review's words travel with the key.
        Assert.Equal(["money-in-float"], task.Notes.Keys);
        Assert.StartsWith("Масштаб маленький", task.Lesson, StringComparison.Ordinal);
        Assert.Contains("accept.fits-context", task.Leaves);
    }

    [Fact]
    public async Task AMediumTask_OffersEveryLiveCard()
    {
        _content.Delete("tasks/invoice-mailer-monthly").Delete("problems/swallowed-error");
        await IngestAsync("second");

        var task = await FindAsync("invoice-mailer-hosted");

        Assert.Equal(["hardcoded-config", "money-in-float", "secrets-in-repo"], task!.OfferedCards);
    }

    [Fact]
    public async Task AnUnpublishedTask_IsStillFound_WithItsKey()
    {
        _content.Delete("tasks/invoice-mailer-hosted");
        await IngestAsync("second");

        var task = await FindAsync("invoice-mailer-hosted");

        Assert.NotNull(task);
        Assert.False(task.Published);
        Assert.Equal("hardcoded-config", Assert.Single(task.Findings).Card);
        Assert.Equal(1, task.Findings[0].Weight);
    }

    [Fact]
    public async Task AnUnknownTask_IsNull()
    {
        Assert.Null(await FindAsync("no-such-task"));
    }

    private async Task<Shared.Contracts.Content.TaskForAttempt?> FindAsync(string slug)
    {
        await using var context = _database.CreateContext();
        return await new TaskForAttemptLookup(context).FindAsync(slug, TestContext.Current.CancellationToken);
    }

    private async Task IngestAsync(string revision)
    {
        await using var context = _database.CreateContext();
        var result = await new ContentIngest(context, TimeProvider.System).IngestAsync(_content.Root, revision, TestContext.Current.CancellationToken);
        Assert.True(result.Ingested, string.Join("\n", result.Report.Issues));
    }
}
