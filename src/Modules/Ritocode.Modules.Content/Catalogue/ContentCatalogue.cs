using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Contracts.Attempts;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Content.Catalogue;

/// <summary>The read side of the content schema (docs/SPEC.md §9.3). Text is in the default locale.</summary>
public interface IContentCatalogue
{
    Task<ProblemCatalogueView> GetProblemsAsync(CancellationToken cancellationToken);

    Task<TreatmentTreeView> GetTreatmentsAsync(CancellationToken cancellationToken);

    /// <summary>A page of the task catalogue; with <paramref name="userId"/>, each task says whether that user solved it.</summary>
    Task<Page<TaskSummaryView>> ListTasksAsync(PageRequest request, Guid? userId, CancellationToken cancellationToken);

    /// <summary>A published task, or <c>task_not_found</c> for a slug that names none.</summary>
    Task<Result<TaskView>> GetTaskAsync(string slug, CancellationToken cancellationToken);
}

internal sealed class ContentCatalogue(ContentDbContext context, ISubmittedTaskLookup submitted) : IContentCatalogue
{
    public const string TaskNotFound = "task_not_found";

    private static readonly string[] DifficultyOrder = ["easy", "medium", "hard"];

    public async Task<ProblemCatalogueView> GetProblemsAsync(CancellationToken cancellationToken)
    {
        var taxonomy = await TaxonomyAsync(cancellationToken).ConfigureAwait(false);
        var labels = Localised(taxonomy.Texts);
        var cards = await LiveCardsAsync(cancellationToken).ConfigureAwait(false);

        var classes = taxonomy.Classes
            .Select(id => labels?.Classes.GetValueOrDefault(id) is { } label
                ? new ClassView(id, label.Name, label.Description)
                : new ClassView(id, id, null))
            .ToList();

        var views = cards
            .OrderBy(card => ClassRank(taxonomy, card.Class))
            .ThenBy(card => card.Slug, StringComparer.Ordinal)
            .Select(card => (card, text: Localised(ContentJson.Read<Dictionary<string, CardText>>(card.Texts))))
            .Where(pair => pair.text is not null)
            .Select(pair => ToView(pair.card, pair.text!))
            .ToList();

        return new ProblemCatalogueView(classes, views);
    }

    public async Task<TreatmentTreeView> GetTreatmentsAsync(CancellationToken cancellationToken)
    {
        var taxonomy = await TaxonomyAsync(cancellationToken).ConfigureAwait(false);
        var labels = Localised(taxonomy.Texts);

        var branches = taxonomy.Branches
            .Select(branch =>
            {
                var text = labels?.Branches.GetValueOrDefault(branch.Id);

                return new BranchView(
                    branch.Id,
                    text?.Name ?? branch.Id,
                    [.. branch.Leaves.Select(leaf => new LeafView($"{branch.Id}.{leaf}", text?.Leaves.GetValueOrDefault(leaf) ?? leaf))]);
            })
            .ToList();

        return new TreatmentTreeView(branches);
    }

    public async Task<Page<TaskSummaryView>> ListTasksAsync(PageRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Twenty tasks in the MVP: ordering and paging in memory keeps the difficulty order readable
        // here rather than encoded in SQL. Revisit when the catalogue is large enough to matter.
        var tasks = (await PublishedTasksAsync(cancellationToken).ConfigureAwait(false))
            .Select(Summary)
            .OrderBy(task => Array.IndexOf(DifficultyOrder, task.Difficulty))
            .ThenBy(task => task.Title, StringComparer.Ordinal)
            .ToList();

        var page = tasks.Skip((int)request.Offset).Take(request.PageSize).ToList();

        if (userId is { } user && page.Count > 0)
        {
            var solved = await submitted.FindSubmittedAsync(user, [.. page.Select(task => task.Slug)], cancellationToken).ConfigureAwait(false);
            page = [.. page.Select(task => task with { Solved = solved.Contains(task.Slug) })];
        }

        return Page<TaskSummaryView>.From(page, request, tasks.Count);
    }

    public async Task<Result<TaskView>> GetTaskAsync(string slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var task = await context.Tasks.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Slug == slug && row.UnpublishedAt == null, cancellationToken)
            .ConfigureAwait(false);

        var text = task is null ? null : Localised(ContentJson.Read<Dictionary<string, TaskText>>(task.Texts));

        if (task is null || text is null)
        {
            return AppError.NotFound(TaskNotFound, $"There is no published task '{slug}'.");
        }

        var material = await context.Materials.AsNoTracking()
            .SingleAsync(row => row.Slug == task.Material, cancellationToken)
            .ConfigureAwait(false);

        var files = ContentJson.Read<List<StoredMaterialFile>>(material.Files)
            .Select(file => new MaterialFileView(file.Path, file.Content))
            .ToList();

        var cards = await CandidatesAsync(task, cancellationToken).ConfigureAwait(false);

        // The six groups step 1 browses by (SPEC §4.4) — their names, never a card's full text.
        var classes = await ClassesAsync(cancellationToken).ConfigureAwait(false);

        var siblings = (await PublishedTasksAsync(cancellationToken).ConfigureAwait(false))
            .Where(row => row.Material == task.Material && row.Slug != task.Slug)
            .Select(Summary)
            .OrderBy(sibling => sibling.Title, StringComparer.Ordinal)
            .ToList();

        return new TaskView(
            task.Slug,
            text.Title,
            task.Difficulty,
            text.Context,
            text.Brief,
            new MaterialView(files, ContentJson.Read<MaterialOverview>(material.Overview)),
            classes,
            cards,
            siblings);
    }

    private async Task<List<ClassView>> ClassesAsync(CancellationToken cancellationToken)
    {
        var taxonomy = await TaxonomyAsync(cancellationToken).ConfigureAwait(false);
        var labels = Localised(taxonomy.Texts);

        return [.. taxonomy.Classes.Select(id => labels?.Classes.GetValueOrDefault(id) is { } label
            ? new ClassView(id, label.Name, label.Description)
            : new ClassView(id, id, null))];
    }

    /// <summary>The shortlist for an easy task, the whole live catalogue for any other (SPEC §4.4).</summary>
    private async Task<List<CandidateCardView>> CandidatesAsync(StoredTask task, CancellationToken cancellationToken)
    {
        var taxonomy = await TaxonomyAsync(cancellationToken).ConfigureAwait(false);
        var cards = await LiveCardsAsync(cancellationToken).ConfigureAwait(false);

        if (task.Shortlist.Length > 0)
        {
            var shortlist = task.Shortlist.ToHashSet(StringComparer.Ordinal);
            cards = [.. cards.Where(card => shortlist.Contains(card.Slug))];
        }

        return cards
            .OrderBy(card => ClassRank(taxonomy, card.Class))
            .ThenBy(card => card.Slug, StringComparer.Ordinal)
            .Select(card => (card, text: Localised(ContentJson.Read<Dictionary<string, CardText>>(card.Texts))))
            .Where(pair => pair.text is not null)
            .Select(pair => new CandidateCardView(pair.card.Slug, pair.card.Class, pair.text!.Name, pair.text.Summary, pair.text.Keywords))
            .ToList();
    }

    private async Task<Taxonomy> TaxonomyAsync(CancellationToken cancellationToken)
    {
        var row = await context.Taxonomy.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? Taxonomy.Empty : ContentJson.Read<Taxonomy>(row.Document);
    }

    private Task<List<StoredCard>> LiveCardsAsync(CancellationToken cancellationToken) =>
        context.Cards.AsNoTracking().Where(row => row.RetiredAt == null).ToListAsync(cancellationToken);

    private Task<List<StoredTask>> PublishedTasksAsync(CancellationToken cancellationToken) =>
        context.Tasks.AsNoTracking().Where(row => row.UnpublishedAt == null).ToListAsync(cancellationToken);

    private static TaskSummaryView Summary(StoredTask task) =>
        new(task.Slug, Localised(ContentJson.Read<Dictionary<string, TaskText>>(task.Texts))?.Title ?? task.Slug, task.Difficulty);

    private static CardView ToView(StoredCard card, CardText text) => new(
        card.Slug,
        card.Class,
        text.Name,
        text.Summary,
        text.Keywords,
        new CardSectionsView(
            text.Sections.GetValueOrDefault(CardSection.Signs),
            text.Sections.GetValueOrDefault(CardSection.WhyAiDoesIt),
            text.Sections.GetValueOrDefault(CardSection.Cost),
            text.Sections.GetValueOrDefault(CardSection.AcceptableWhen),
            text.Sections.GetValueOrDefault(CardSection.Detection),
            text.Sections.GetValueOrDefault(CardSection.Treatment),
            text.Sections.GetValueOrDefault(CardSection.Sources),
            text.Sections.GetValueOrDefault(CardSection.CounterArguments)));

    private static int ClassRank(Taxonomy taxonomy, string id)
    {
        var rank = taxonomy.Classes.ToList().IndexOf(id);
        return rank < 0 ? int.MaxValue : rank;
    }

    private static T? Localised<T>(IReadOnlyDictionary<string, T> texts)
        where T : class =>
        texts.GetValueOrDefault(ContentRules.DefaultLocale);
}
