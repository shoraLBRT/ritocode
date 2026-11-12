using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Attempts.Progress;

/// <summary>A learner's progress (docs/SPEC.md §4.7): per class, then per card. No XP, no levels.</summary>
/// <param name="Tasks">How many tasks the progress is built from — one first attempt each.</param>
/// <param name="NotRevealed">
/// How many of those tasks the per-card table leaves out because the learner has not revealed their
/// answer (ADR 0017): the classes count them, the cards do not.
/// </param>
public sealed record ProgressView(int Tasks, int NotRevealed, IReadOnlyList<ClassProgress> Classes, IReadOnlyList<CardProgress> Cards);

/// <summary>How many findings of the class were met, how many found, and how many found ones were treated right.</summary>
public sealed record ClassProgress(string Class, string Name, int Met, int Found, int TreatedRight);

/// <summary>
/// One card across the learner's first attempts at tasks whose answer they have revealed: in how many
/// keys it was met, found and missed; how often it was picked where the key did not list it; and how
/// often, found, it was treated right. A card Content no longer knows is named by its slug and has no
/// class.
/// </summary>
public sealed record CardProgress(string Card, string Name, string? Class, int Met, int Found, int Missed, int PickedWhenAbsent, int TreatedRight);

/// <summary>The scored result of a learner's first attempt at a task, and whether they have revealed the task's answer.</summary>
public sealed record FirstAttempt(DiagnosisScore Score, bool AnswerRevealed);

/// <summary>
/// Progress as a pure function of the scored results of **first** attempts only (SPEC §5.4) — the
/// caller passes nothing else, so practice cannot move it — whether each task's answer is revealed,
/// and what Content says of the cards: the class of each, and the names.
/// </summary>
/// <remarks>
/// The classes count every first attempt; the cards only those at tasks whose answer is revealed, so
/// the per-card table never names a card of a key the learner has not chosen to see (ADR 0017). A
/// reveal moves a task into the table and changes nothing else: the score stays the first attempt's.
/// </remarks>
public static class ProgressCalculator
{
    public static ProgressView Calculate(
        IReadOnlyCollection<FirstAttempt> firstAttempts,
        CardClasses catalogue)
    {
        ArgumentNullException.ThrowIfNull(firstAttempts);
        ArgumentNullException.ThrowIfNull(catalogue);

        var classes = catalogue.Classes;

        var cards = firstAttempts
            .Where(attempt => attempt.AnswerRevealed)
            .SelectMany(attempt => attempt.Score.Cards)
            .GroupBy(line => line.Card, StringComparer.Ordinal)
            .Select(group => new CardProgress(
                group.Key,
                catalogue.CardNames.GetValueOrDefault(group.Key) ?? group.Key,
                catalogue.ClassOf.GetValueOrDefault(group.Key),
                Met: group.Count(Met),
                Found: group.Count(Found),
                Missed: group.Count(line => line.Outcome == CardOutcome.Missed),
                PickedWhenAbsent: group.Count(line => line.Outcome == CardOutcome.Extra),
                TreatedRight: group.Count(TreatedRight)))
            .OrderBy(card => card.Class is null ? int.MaxValue : IndexOf(classes, card.Class))
            .ThenBy(card => card.Card, StringComparer.Ordinal)
            .ToList();

        var byClass = firstAttempts
            .SelectMany(attempt => attempt.Score.Cards)
            .ToLookup(line => catalogue.ClassOf.GetValueOrDefault(line.Card) ?? string.Empty, StringComparer.Ordinal);

        var perClass = classes
            .Select(@class => new ClassProgress(
                @class,
                catalogue.ClassNames.GetValueOrDefault(@class) ?? @class,
                byClass[@class].Count(Met),
                byClass[@class].Count(Found),
                byClass[@class].Count(TreatedRight)))
            .ToList();

        return new ProgressView(
            firstAttempts.Count,
            firstAttempts.Count(attempt => !attempt.AnswerRevealed),
            perClass,
            cards);
    }

    private static bool Met(CardScore line) => line.Outcome != CardOutcome.Extra;

    private static bool Found(CardScore line) => line.Outcome == CardOutcome.Found;

    private static bool TreatedRight(CardScore line) => line.Outcome == CardOutcome.Found && line.Treatment?.Matched == true;

    private static int IndexOf(IReadOnlyList<string> classes, string @class)
    {
        for (var index = 0; index < classes.Count; index++)
        {
            if (string.Equals(classes[index], @class, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
