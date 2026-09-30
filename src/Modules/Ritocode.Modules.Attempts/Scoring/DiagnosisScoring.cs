namespace Ritocode.Modules.Attempts.Scoring;

/// <summary>A learner's answer: the cards picked in step 1 and, for each, the leaves ticked in step 2.</summary>
public sealed record DiagnosisAnswer(IReadOnlyList<PickedCard> Picks);

/// <summary>One picked card and its leaves, each addressed as <c>branch.leaf</c>.</summary>
public sealed record PickedCard(string Card, IReadOnlyList<string> Leaves);

/// <summary>
/// One finding of the answer key as scoring needs it: the card, the card's weight (it belongs to the
/// card, never to the finding — SPEC §3.2), and the leaves right for it in this context, any of them.
/// </summary>
public sealed record KeyFinding(string Card, int Weight, IReadOnlyList<string> Leaves);

/// <summary>
/// The score of one answer: the total (floored at zero), the maximum, and one line per card — the
/// key's findings in the key's order, then the extra picks by slug.
/// </summary>
/// <param name="IsCorrect">
/// Nothing was lost: every finding found with a right leaf, no wrong leaf, no extra pick. On a clean
/// task that is an answer that picked nothing — 0 of 0, and correct.
/// </param>
public sealed record DiagnosisScore(int Total, int Maximum, bool IsCorrect, IReadOnlyList<CardScore> Cards);

public enum CardOutcome
{
    Found,
    Missed,
    Extra,
}

/// <summary>
/// One card of the review and the points it brought. <see cref="Treatment"/> is set for a found card
/// only: an extra card's leaves are not scored, there being no key to compare them with.
/// </summary>
public sealed record CardScore(string Card, CardOutcome Outcome, int Points, TreatmentScore? Treatment);

/// <summary>The treatment of a found card: the picked leaves split by the key, and the key's own leaves.</summary>
public sealed record TreatmentScore(
    bool Matched,
    IReadOnlyList<string> MatchedLeaves,
    IReadOnlyList<string> WrongLeaves,
    IReadOnlyList<string> KeyLeaves);

/// <summary>
/// The scoring of a diagnosis (docs/SPEC.md §5): a pure function of the answer, the answer key with
/// its card weights, and the parameters. The same inputs always give the same score, whatever the
/// order of the picks or of the leaves.
/// </summary>
public static class DiagnosisScoring
{
    public static DiagnosisScore Score(DiagnosisAnswer answer, IReadOnlyList<KeyFinding> key, ScoringParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(parameters);

        var picks = ByCard(answer.Picks, pick => pick.Card, nameof(answer));
        var findings = ByCard(key, finding => finding.Card, nameof(key));

        var lines = new List<CardScore>();

        foreach (var finding in key)
        {
            if (finding.Weight < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(key), finding.Weight, $"The weight of '{finding.Card}' must be positive.");
            }

            lines.Add(picks.TryGetValue(finding.Card, out var pick)
                ? Found(finding, pick, parameters)
                : new CardScore(finding.Card, CardOutcome.Missed, -parameters.Missed * finding.Weight, null));
        }

        lines.AddRange(answer.Picks
            .Where(pick => !findings.ContainsKey(pick.Card))
            .OrderBy(pick => pick.Card, StringComparer.Ordinal)
            .Select(pick => new CardScore(pick.Card, CardOutcome.Extra, -parameters.Extra, null)));

        var raw = lines.Sum(line => line.Points);
        var maximum = key.Sum(finding => (parameters.Found + parameters.TreatmentMatched) * finding.Weight);

        return new DiagnosisScore(Math.Max(0, raw), maximum, raw == maximum, lines);
    }

    private static CardScore Found(KeyFinding finding, PickedCard pick, ScoringParameters parameters)
    {
        var keyLeaves = finding.Leaves.ToHashSet(StringComparer.Ordinal);
        var picked = pick.Leaves.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        List<string> matched = [.. picked.Where(keyLeaves.Contains)];
        List<string> wrong = [.. picked.Where(leaf => !keyLeaves.Contains(leaf))];

        var points = (parameters.Found * finding.Weight)
            + (matched.Count > 0 ? parameters.TreatmentMatched * finding.Weight : 0)
            - (parameters.WrongLeaf * wrong.Count);

        return new CardScore(
            finding.Card,
            CardOutcome.Found,
            points,
            new TreatmentScore(matched.Count > 0, matched, wrong, [.. keyLeaves.Order(StringComparer.Ordinal)]));
    }

    private static Dictionary<string, T> ByCard<T>(IEnumerable<T> items, Func<T, string> card, string parameter)
    {
        var byCard = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!byCard.TryAdd(card(item), item))
            {
                throw new ArgumentException($"The card '{card(item)}' appears more than once.", parameter);
            }
        }

        return byCard;
    }
}
