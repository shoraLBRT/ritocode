using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Modules.Content.Tests.Format;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Content.Tests.Ingest;

/// <summary>Ingest against a real PostgreSQL: what it writes, and what it does when content moves.</summary>
public sealed class ContentIngestTests(PostgresTestServer postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly TestClock _clock = new(Start);
    private ContentDatabase _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await ContentDatabase.CreateAsync(postgres, nameof(ContentIngestTests));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Ingest_WritesEveryItem_StampedWithTheRevision()
    {
        using var content = TempContent.FromReference();

        var result = await IngestAsync(content, "abc1234");

        Assert.True(result.Ingested);
        Assert.Equal((4, 1, 1), (result.Cards, result.Materials, result.Tasks));

        await using var context = _database.CreateContext();
        var card = await context.Cards.SingleAsync(row => row.Slug == "secrets-in-repo", TestContext.Current.CancellationToken);
        Assert.Equal(("hygiene", 3, "abc1234"), (card.Class, card.Weight, card.ContentRevision));
        Assert.Null(card.RetiredAt);
        Assert.Contains("Секреты в репозитории", card.Texts, StringComparison.Ordinal);

        var task = await context.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(("invoice-mailer", "easy"), (task.Material, task.Difficulty));
        Assert.Contains("auto.secrets", task.Findings, StringComparison.Ordinal);
        Assert.Null(task.UnpublishedAt);

        var taxonomy = await context.Taxonomy.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(StoredTaxonomy.SingletonId, taxonomy.Id);
        Assert.Contains("fits-context", taxonomy.Document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ingest_DerivesTheMaterialOverview()
    {
        using var content = TempContent.FromReference()
            .Write("materials/invoice-mailer/files/requirements.txt", "requests==2.32.0\n# a comment\nrich>=13\n");

        await IngestAsync(content, "abc1234");

        await using var context = _database.CreateContext();
        var material = await context.Materials.SingleAsync(TestContext.Current.CancellationToken);
        var overview = ReadOverview(material.Overview);

        Assert.Equal(2, overview.FileCount);
        Assert.Equal(23, overview.TotalLines);
        Assert.Equal(["invoice.py", "requirements.txt"], overview.Files.Select(file => file.Path));
        Assert.Equal(["requests", "rich"], overview.Dependencies);
    }

    [Fact]
    public async Task Ingest_GivesAnEasyTaskItsFindingsInTheShortlist_AndItIsStable()
    {
        using var content = TempContent.FromReference();

        await IngestAsync(content, "first");
        var first = await ShortlistAsync();

        await IngestAsync(content, "second");
        var second = await ShortlistAsync();

        Assert.Equal(["hardcoded-config", "money-in-float", "secrets-in-repo", "swallowed-error"], first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task IngestingTwice_IsIdempotent_AndAnEditIsApplied()
    {
        using var content = TempContent.FromReference();
        await IngestAsync(content, "first");

        content.Write("problems/secrets-in-repo/card.yaml", "class: hygiene\nweight: 2\n");
        var result = await IngestAsync(content, "second");

        Assert.True(result.Ingested);
        await using var context = _database.CreateContext();
        Assert.Equal(4, await context.Cards.CountAsync(TestContext.Current.CancellationToken));
        var card = await context.Cards.SingleAsync(row => row.Slug == "secrets-in-repo", TestContext.Current.CancellationToken);
        Assert.Equal((2, "second"), (card.Weight, card.ContentRevision));
    }

    [Fact]
    public async Task ARemovedTask_IsUnpublished_AndARemovedCard_IsRetired_NeverDeleted()
    {
        using var content = TempContent.FromReference();
        await IngestAsync(content, "first");

        _clock.Advance(TimeSpan.FromHours(1));
        content.Delete("tasks/invoice-mailer-monthly").Delete("problems/hardcoded-config");
        var result = await IngestAsync(content, "second");

        Assert.Equal((1, 1), (result.CardsRetired, result.TasksUnpublished));

        await using var context = _database.CreateContext();
        var task = await context.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Start.AddHours(1), task.UnpublishedAt);
        var card = await context.Cards.SingleAsync(row => row.Slug == "hardcoded-config", TestContext.Current.CancellationToken);
        Assert.Equal(Start.AddHours(1), card.RetiredAt);

        // Coming back publishes and un-retires them.
        content.Write("problems/hardcoded-config/card.yaml", "class: hygiene\nweight: 1\n")
            .Write("problems/hardcoded-config/ru.md", File.ReadAllText(Path.Combine(TempContent.ReferenceRoot, "problems", "hardcoded-config", "ru.md")));
        await IngestAsync(content, "third");

        await using var after = _database.CreateContext();
        Assert.Null((await after.Cards.SingleAsync(row => row.Slug == "hardcoded-config", TestContext.Current.CancellationToken)).RetiredAt);
    }

    [Fact]
    public async Task ContentWithErrors_WritesNothing()
    {
        using var content = TempContent.FromReference().Write("problems/secrets-in-repo/card.yaml", "class: nowhere\nweight: 3\n");

        var result = await IngestAsync(content, "abc1234");

        Assert.False(result.Ingested);
        Assert.True(result.Report.HasErrors);
        await using var context = _database.CreateContext();
        Assert.False(await context.Cards.AnyAsync(TestContext.Current.CancellationToken));
        Assert.False(await context.Taxonomy.AnyAsync(TestContext.Current.CancellationToken));
    }

    private async Task<ContentIngestResult> IngestAsync(TempContent content, string revision)
    {
        await using var context = _database.CreateContext();
        var ingest = new ContentIngest(context, _clock);
        return await ingest.IngestAsync(content.Root, revision, TestContext.Current.CancellationToken);
    }

    private async Task<string[]> ShortlistAsync()
    {
        await using var context = _database.CreateContext();
        return (await context.Tasks.SingleAsync(TestContext.Current.CancellationToken)).Shortlist;
    }

    private static MaterialOverview ReadOverview(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<MaterialOverview>(json, ContentJson.Options)!;
}
