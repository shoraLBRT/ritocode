using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Persistence;

namespace Ritocode.Modules.Content.Ingest;

/// <summary>Loads a content tree into the <c>content</c> schema.</summary>
public interface IContentIngest
{
    /// <summary>
    /// Validates the tree at <paramref name="root"/> and, only when it has no errors, writes it in one
    /// transaction stamped with <paramref name="revision"/> — the commit it came from.
    /// </summary>
    Task<ContentIngestResult> IngestAsync(string root, string revision, CancellationToken cancellationToken);
}

/// <summary>What an ingest did. <see cref="Ingested"/> is false when the report has errors and nothing was written.</summary>
public sealed record ContentIngestResult(
    ContentReport Report,
    bool Ingested,
    int Cards,
    int Materials,
    int Tasks,
    int CardsRetired,
    int TasksUnpublished);

/// <remarks>
/// Upserts by slug. A task that left <c>content/</c> is unpublished and a card is retired — never
/// deleted, because attempts name them. A material is only ever upserted: an unpublished task's
/// attempts still show its code.
/// </remarks>
internal sealed class ContentIngest(ContentDbContext context, TimeProvider clock) : IContentIngest
{
    public async Task<ContentIngestResult> IngestAsync(string root, string revision, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);

        var (content, report) = ContentLoader.Load(root);

        if (report.HasErrors)
        {
            return new ContentIngestResult(report, Ingested: false, 0, 0, 0, 0, 0);
        }

        var now = clock.GetUtcNow();

        // The host's contexts retry transient failures, and a retrying strategy refuses a transaction
        // opened by hand: the whole unit goes through the strategy, and a retry starts from a clean
        // change tracker so nothing from the failed attempt is written twice.
        var strategy = context.Database.CreateExecutionStrategy();

        var (retired, unpublished) = await strategy.ExecuteAsync(
            async token =>
            {
                context.ChangeTracker.Clear();

                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);

                await WriteTaxonomyAsync(content.Taxonomy, revision, now, token).ConfigureAwait(false);
                var retiredCards = await WriteCardsAsync(content.Cards, revision, now, token).ConfigureAwait(false);
                await WriteMaterialsAsync(content.Materials, revision, now, token).ConfigureAwait(false);
                var unpublishedTasks = await WriteTasksAsync(content, revision, now, token).ConfigureAwait(false);

                await context.SaveChangesAsync(token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);

                return (retiredCards, unpublishedTasks);
            },
            cancellationToken).ConfigureAwait(false);

        return new ContentIngestResult(
            report,
            Ingested: true,
            content.Cards.Count,
            content.Materials.Count,
            content.Tasks.Count,
            retired,
            unpublished);
    }

    private async Task WriteTaxonomyAsync(Taxonomy taxonomy, string revision, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await context.Taxonomy.FindAsync([StoredTaxonomy.SingletonId], cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            row = new StoredTaxonomy();
            context.Taxonomy.Add(row);
        }

        row.Document = ContentJson.Write(taxonomy);
        row.ContentRevision = revision;
        row.UpdatedAt = now;
    }

    private async Task<int> WriteCardsAsync(IReadOnlyList<ProblemCard> cards, string revision, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await context.Cards.ToDictionaryAsync(row => row.Slug, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        foreach (var card in cards)
        {
            if (!rows.TryGetValue(card.Slug, out var row))
            {
                row = new StoredCard { Slug = card.Slug };
                context.Cards.Add(row);
            }

            row.Class = card.Class;
            row.Weight = card.Weight;
            row.Texts = ContentJson.Write(card.Texts);
            row.RetiredAt = null;
            row.ContentRevision = revision;
            row.UpdatedAt = now;
        }

        var present = cards.Select(card => card.Slug).ToHashSet(StringComparer.Ordinal);
        var retired = 0;

        foreach (var row in rows.Values.Where(row => row.RetiredAt is null && !present.Contains(row.Slug)))
        {
            row.RetiredAt = now;
            row.ContentRevision = revision;
            row.UpdatedAt = now;
            retired++;
        }

        return retired;
    }

    private async Task WriteMaterialsAsync(IReadOnlyList<Material> materials, string revision, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await context.Materials.ToDictionaryAsync(row => row.Slug, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        foreach (var material in materials)
        {
            if (!rows.TryGetValue(material.Slug, out var row))
            {
                row = new StoredMaterial { Slug = material.Slug };
                context.Materials.Add(row);
            }

            row.Language = material.Language;
            row.Files = ContentJson.Write(material.Files.Select(file => new StoredMaterialFile(file.Path, file.Content)));
            row.Overview = ContentJson.Write(MaterialOverview.Of(material));
            row.ContentRevision = revision;
            row.UpdatedAt = now;
        }
    }

    private async Task<int> WriteTasksAsync(ContentSet content, string revision, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await context.Tasks.ToDictionaryAsync(row => row.Slug, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        var catalogue = content.Cards.Select(card => card.Slug).ToList();

        foreach (var task in content.Tasks)
        {
            if (!rows.TryGetValue(task.Slug, out var row))
            {
                row = new StoredTask { Slug = task.Slug };
                context.Tasks.Add(row);
            }

            var findings = task.Findings.Select(finding => finding.Card).ToList();

            row.Material = task.Material;
            row.Difficulty = DifficultyName(task.Difficulty);
            row.Findings = ContentJson.Write(task.Findings);
            row.Texts = ContentJson.Write(task.Texts);
            row.Shortlist = task.Difficulty == TaskDifficulty.Easy ? [.. Shortlist.For(task.Slug, findings, catalogue)] : [];
            row.UnpublishedAt = null;
            row.ContentRevision = revision;
            row.UpdatedAt = now;
        }

        var present = content.Tasks.Select(task => task.Slug).ToHashSet(StringComparer.Ordinal);
        var unpublished = 0;

        foreach (var row in rows.Values.Where(row => row.UnpublishedAt is null && !present.Contains(row.Slug)))
        {
            row.UnpublishedAt = now;
            row.ContentRevision = revision;
            row.UpdatedAt = now;
            unpublished++;
        }

        return unpublished;
    }

    private static string DifficultyName(TaskDifficulty difficulty) => difficulty switch
    {
        TaskDifficulty.Easy => "easy",
        TaskDifficulty.Medium => "medium",
        TaskDifficulty.Hard => "hard",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };
}

/// <summary>One file of a material as the <c>materials.files</c> column holds it.</summary>
public sealed record StoredMaterialFile(string Path, string Content);
