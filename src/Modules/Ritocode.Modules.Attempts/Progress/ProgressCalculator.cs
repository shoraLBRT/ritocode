using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Progress;

/// <summary>A learner's progress (docs/SPEC.md §4.7): per class, then per card. No XP, no levels.</summary>
/// <param name="Tasks">How many tasks the progress is built from — one first attempt each.</param>
public sealed record ProgressView(int Tasks, IReadOnlyList<ClassProgress> Classes, IReadOnlyList<CardProgress> Cards);

/// <summary>How many findings of the class were met, how many found, and how many found ones were treated right.</summary>
public sealed record ClassProgress(string Class, int Met, int Found, int TreatedRight);

/// <summary>
/// One card across the learner's first attempts: in how many keys it was met, found and missed; how
/// often it was picked where the key did not list it; and how often, found, it was treated right.
/// </summary>
public sealed record CardProgress(string Card, string? Class, int Met, int Found, int Missed, int PickedWhenAbsent, int TreatedRight);

/// <summary>
/// Progress as a pure function of the scored results of **first** attempts only (SPEC §5.4) — the
/// caller passes nothing else, so practice cannot move it — and the class of each card.
/// </summary>
public static class ProgressCalculator
{
    public static ProgressView Calculate(
        IReadOnlyCollection<DiagnosisScore> firstAttempts,
        IReadOnlyList<string> classes,
        IReadOnlyDictionary<string, string> classOf)
    {
        ArgumentNullException.ThrowIfNull(firstAttempts);
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(classOf);

        var lines = firstAttempts.SelectMany(score => score.Cards).ToList();

        var cards = lines
            .GroupBy(line => line.Card, StringComparer.Ordinal)
            .Select(group => new CardProgress(
                group.Key,
                classOf.GetValueOrDefault(group.Key),
                Met: group.Count(line => line.Outcome != CardOutcome.Extra),
                Found: group.Count(line => line.Outcome == CardOutcome.Found),
                Missed: group.Count(line => line.Outcome == CardOutcome.Missed),
                PickedWhenAbsent: group.Count(line => line.Outcome == CardOutcome.Extra),
                TreatedRight: group.Count(TreatedRight)))
            .OrderBy(card => card.Class is null ? int.MaxValue : IndexOf(classes, card.Class))
            .ThenBy(card => card.Card, StringComparer.Ordinal)
            .ToList();

        var perClass = classes
            .Select(@class =>
            {
                var inClass = cards.Where(card => card.Class == @class).ToList();
                return new ClassProgress(@class, inClass.Sum(card => card.Met), inClass.Sum(card => card.Found), inClass.Sum(card => card.TreatedRight));
            })
            .ToList();

        return new ProgressView(firstAttempts.Count, perClass, cards);
    }

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
