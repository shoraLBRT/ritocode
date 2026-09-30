using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Content.Contracts;

/// <summary>
/// The Content module's answer to <see cref="ITaskForAttemptLookup"/>, over its own schema: the one
/// place the answer key and the card weights leave this module.
/// </summary>
internal sealed class TaskForAttemptLookup(ContentDbContext context) : ITaskForAttemptLookup
{
    public async Task<TaskForAttempt?> FindAsync(string taskSlug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskSlug);

        var task = await context.Tasks.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Slug == taskSlug, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            return null;
        }

        var findings = ContentJson.Read<List<Finding>>(task.Findings);
        var keyCards = findings.Select(finding => finding.Card).ToList();

        // Retired cards included: a task unpublished with its card still has a key to score against.
        var weights = await context.Cards.AsNoTracking()
            .Where(card => keyCards.Contains(card.Slug))
            .ToDictionaryAsync(card => card.Slug, card => card.Weight, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var offered = task.Shortlist.Length > 0
            ? [.. task.Shortlist]
            : await context.Cards.AsNoTracking()
                .Where(card => card.RetiredAt == null)
                .Select(card => card.Slug)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var taxonomy = await context.Taxonomy.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var leaves = taxonomy is null ? [] : ContentJson.Read<Taxonomy>(taxonomy.Document).LeafIds.ToList();

        // The review's words, which leave with the key and only with it.
        var text = ContentJson.Read<Dictionary<string, TaskText>>(task.Texts).GetValueOrDefault(ContentRules.DefaultLocale);

        return new TaskForAttempt(
            task.Slug,
            task.UnpublishedAt is null,
            task.ContentRevision,
            [.. findings.Select(finding => new TaskFinding(finding.Card, Weight(finding.Card), finding.Leaves))],
            [.. offered.Order(StringComparer.Ordinal)],
            leaves,
            text?.Notes.ToDictionary(note => note.Key, note => note.Value.Trim(), StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            text?.Lesson?.Trim());

        // Validation refuses a key naming a card that does not exist, and a card is never deleted.
        int Weight(string card) => weights.TryGetValue(card, out var weight)
            ? weight
            : throw new InvalidOperationException($"Task '{task.Slug}' names the card '{card}', which has no row.");
    }
}
