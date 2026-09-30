using Ritocode.Modules.Content.Contracts;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Tests.Format;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Content.Tests.Contracts;

/// <summary>What the Attempts module learns about cards to group progress by class.</summary>
public sealed class CardClassLookupTests(PostgresTestServer postgres) : IAsyncLifetime
{
    private ContentDatabase _database = null!;
    private TempContent _content = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await ContentDatabase.CreateAsync(postgres, nameof(CardClassLookupTests));
        _content = TempContent.FromReference();
        await IngestAsync("first");
    }

    public ValueTask DisposeAsync()
    {
        _content.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheClassOfEachKnownCard_AndEveryClassInOrder()
    {
        await using var context = _database.CreateContext();

        var found = await new CardClassLookup(context).FindAsync(["money-in-float", "secrets-in-repo", "no-such-card"], TestContext.Current.CancellationToken);

        Assert.Equal(["disproportion", "project-knowledge", "hygiene", "growth", "false-confidence", "domain"], found.Classes);
        Assert.Equal("domain", found.ClassOf["money-in-float"]);
        Assert.Equal("hygiene", found.ClassOf["secrets-in-repo"]);
        Assert.False(found.ClassOf.ContainsKey("no-such-card"));
    }

    [Fact]
    public async Task TheNamesOfEveryClassAndOfEachKnownCard_InTheDefaultLocale()
    {
        await using var context = _database.CreateContext();

        var found = await new CardClassLookup(context).FindAsync(["money-in-float", "no-such-card"], TestContext.Current.CancellationToken);

        Assert.Equal(6, found.ClassNames.Count);
        Assert.Equal("Предметная область", found.ClassNames["domain"]);
        Assert.Equal(["money-in-float"], found.CardNames.Keys);
        Assert.Equal("Деньги не в десятичном типе", found.CardNames["money-in-float"]);
    }

    [Fact]
    public async Task ARetiredCard_StillHasItsClass()
    {
        _content.Delete("tasks/invoice-mailer-monthly").Delete("problems/hardcoded-config");
        await IngestAsync("second");
        await using var context = _database.CreateContext();

        var found = await new CardClassLookup(context).FindAsync(["hardcoded-config"], TestContext.Current.CancellationToken);

        Assert.Equal("hygiene", found.ClassOf["hardcoded-config"]);
        Assert.Equal("Хардкод конфигурации", found.CardNames["hardcoded-config"]);
    }

    private async Task IngestAsync(string revision)
    {
        await using var context = _database.CreateContext();
        var result = await new ContentIngest(context, TimeProvider.System).IngestAsync(_content.Root, revision, TestContext.Current.CancellationToken);
        Assert.True(result.Ingested, string.Join("\n", result.Report.Issues));
    }
}
