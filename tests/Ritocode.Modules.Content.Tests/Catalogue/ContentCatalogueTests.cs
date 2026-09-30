using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Ritocode.Modules.Content.Catalogue;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Tests.Format;
using Ritocode.Shared.Contracts.Attempts;
using Ritocode.Shared.Paging;
using Ritocode.TestSupport;

namespace Ritocode.Modules.Content.Tests.Catalogue;

/// <summary>The read side over ingested reference content, against a real PostgreSQL.</summary>
public sealed class ContentCatalogueTests(PostgresTestServer postgres) : IAsyncLifetime
{
    private const string Task = "invoice-mailer-monthly";

    private ContentDatabase _database = null!;
    private TempContent _content = null!;

    public async ValueTask InitializeAsync()
    {
        _database = await ContentDatabase.CreateAsync(postgres, nameof(ContentCatalogueTests));

        // A second task over the same material, medium, so it offers the whole catalogue.
        _content = TempContent.FromReference()
            .Write("tasks/invoice-mailer-hosted/task.yaml", "material: invoice-mailer\ndifficulty: medium\nfindings:\n  - card: hardcoded-config\n    leaves: [manual.extract-config]\n")
            .Write("tasks/invoice-mailer-hosted/ru.md", "---\ntitle: Счета для сотни студий\n---\n\n## Context\n\nСервис для сотни студий.\n\n## Brief\n\n«То же самое.»\n");

        await IngestAsync();
    }

    public ValueTask DisposeAsync()
    {
        _content.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheProblemCatalogue_HoldsEveryLiveCard_InFull_GroupedInClassOrder()
    {
        var catalogue = await Catalogue().GetProblemsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(6, catalogue.Classes.Count);
        Assert.Equal("Гигиена и безопасность", catalogue.Classes.Single(@class => @class.Id == "hygiene").Name);

        // Hygiene comes before domain in the taxonomy, so its cards come first.
        Assert.Equal(["hardcoded-config", "secrets-in-repo", "swallowed-error", "money-in-float"], catalogue.Cards.Select(card => card.Slug));

        var secrets = catalogue.Cards.Single(card => card.Slug == "secrets-in-repo");
        Assert.Equal("Никогда.", secrets.Sections.AcceptableWhen);
        Assert.NotNull(secrets.Sections.Cost);
        Assert.Contains("пароль", secrets.Keywords);
    }

    [Fact]
    public async Task ARetiredCard_LeavesTheCatalogue()
    {
        _content.Delete("problems/hardcoded-config").Delete("tasks/invoice-mailer-hosted");
        await IngestAsync();

        var catalogue = await Catalogue().GetProblemsAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(catalogue.Cards, card => card.Slug == "hardcoded-config");
    }

    [Fact]
    public async Task TheTreatmentTree_AddressesLeavesAsBranchDotLeaf_WithLabels()
    {
        var tree = await Catalogue().GetTreatmentsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["brief", "rule", "auto", "manual", "accept"], tree.Branches.Select(branch => branch.Id));
        var accept = tree.Branches.Single(branch => branch.Id == "accept");
        Assert.Equal("Оставить осознанно", accept.Name);
        Assert.Contains(accept.Leaves, leaf => leaf.Id == "accept.fits-context" && leaf.Label == "в этом контексте это нормально");
    }

    [Fact]
    public async Task TheTaskCatalogue_ListsPublishedTasks_EasyFirst()
    {
        var page = await Catalogue().ListTasksAsync(PageRequest.Create(null, null).Value, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalItems);
        Assert.Equal([("invoice-mailer-monthly", "easy"), ("invoice-mailer-hosted", "medium")], page.Items.Select(task => (task.Slug, task.Difficulty)));
        Assert.Equal("Счёт клиенту по почте", page.Items[0].Title);
        Assert.All(page.Items, task => Assert.Null(task.Solved));
    }

    [Fact]
    public async Task ForASignedInCaller_EachTaskSaysWhetherTheySolvedIt()
    {
        var user = Guid.CreateVersion7();
        var submitted = new SubmittedTasks(user, "invoice-mailer-hosted");

        var page = await Catalogue(submitted).ListTasksAsync(PageRequest.Create(null, null).Value, user, TestContext.Current.CancellationToken);

        Assert.Equal([("invoice-mailer-monthly", false), ("invoice-mailer-hosted", true)], page.Items.Select(task => (task.Slug, task.Solved!.Value)));
        Assert.Equal(["invoice-mailer-monthly", "invoice-mailer-hosted"], submitted.Asked);
    }

    [Fact]
    public async Task ATask_CarriesContextBriefMaterialAndItsSiblings()
    {
        var task = (await Catalogue().GetTaskAsync(Task, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("Счёт клиенту по почте", task.Title);
        Assert.StartsWith("Скрипт бухгалтера", task.Context, StringComparison.Ordinal);
        Assert.StartsWith("«Напиши скрипт", task.Brief, StringComparison.Ordinal);
        Assert.Equal("invoice.py", Assert.Single(task.Material.Files).Path);
        Assert.Equal(20, task.Material.Overview.TotalLines);
        Assert.Equal("invoice-mailer-hosted", Assert.Single(task.SameMaterial).Slug);
    }

    [Fact]
    public async Task AnEasyTask_OffersItsShortlist_AndAMediumTask_TheWholeCatalogue()
    {
        var easy = (await Catalogue().GetTaskAsync(Task, TestContext.Current.CancellationToken)).Value;
        var medium = (await Catalogue().GetTaskAsync("invoice-mailer-hosted", TestContext.Current.CancellationToken)).Value;

        // The reference catalogue is four cards, so the shortlist is all of them.
        Assert.Equal(4, easy.Cards.Count);
        Assert.Equal(4, medium.Cards.Count);
        Assert.Equal("Секреты в репозитории", easy.Cards.Single(card => card.Slug == "secrets-in-repo").Name);
    }

    [Fact]
    public async Task ATask_NeverCarriesItsAnswerKey_OrACardsFullText()
    {
        var task = (await Catalogue().GetTaskAsync(Task, TestContext.Current.CancellationToken)).Value;

        // Serialised as the API serialises it, so a field added to a view is caught here too.
        var json = JsonSerializer.Serialize(task, new JsonOptions().SerializerOptions);

        foreach (var leaked in new[] { "findings", "leaves", "auto.secrets", "weight", "sections", "acceptableWhen", "Никогда.", "lesson", "notes" })
        {
            Assert.DoesNotContain(leaked, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AnUnpublishedTask_OrAnUnknownSlug_IsTaskNotFound()
    {
        _content.Delete("tasks/invoice-mailer-hosted");
        await IngestAsync();

        foreach (var slug in new[] { "invoice-mailer-hosted", "no-such-task" })
        {
            var result = await Catalogue().GetTaskAsync(slug, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("task_not_found", result.Error!.Code);
        }
    }

    [Fact]
    public async Task AnEmptyDatabase_AnswersEmptyViews_NotErrors()
    {
        var empty = await ContentDatabase.CreateAsync(postgres, nameof(AnEmptyDatabase_AnswersEmptyViews_NotErrors));
        await using var context = empty.CreateContext();
        var catalogue = new ContentCatalogue(context, new SubmittedTasks(Guid.Empty));

        Assert.Empty((await catalogue.GetProblemsAsync(TestContext.Current.CancellationToken)).Cards);
        Assert.Empty((await catalogue.GetTreatmentsAsync(TestContext.Current.CancellationToken)).Branches);
        Assert.Equal(0, (await catalogue.ListTasksAsync(PageRequest.Create(null, null).Value, Guid.CreateVersion7(), TestContext.Current.CancellationToken)).TotalItems);
    }

    private ContentCatalogue Catalogue(SubmittedTasks? submitted = null) =>
        new(_database.CreateContext(), submitted ?? new SubmittedTasks(Guid.Empty));

    private async Task IngestAsync()
    {
        await using var context = _database.CreateContext();
        var result = await new ContentIngest(context, TimeProvider.System).IngestAsync(_content.Root, "test", TestContext.Current.CancellationToken);
        Assert.True(result.Ingested, string.Join("\n", result.Report.Issues));
    }

    /// <summary>The Attempts module's answer, stood in for: one user, and the tasks they submitted.</summary>
    private sealed class SubmittedTasks(Guid user, params string[] slugs) : ISubmittedTaskLookup
    {
        public List<string> Asked { get; } = [];

        public System.Threading.Tasks.Task<IReadOnlySet<string>> FindSubmittedAsync(Guid userId, IReadOnlyCollection<string> taskSlugs, CancellationToken cancellationToken)
        {
            Asked.AddRange(taskSlugs);
            IReadOnlySet<string> found = userId == user ? taskSlugs.Intersect(slugs).ToHashSet() : new HashSet<string>();
            return System.Threading.Tasks.Task.FromResult(found);
        }
    }
}
