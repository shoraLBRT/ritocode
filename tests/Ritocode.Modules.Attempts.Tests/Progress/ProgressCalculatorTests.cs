using Ritocode.Modules.Attempts.Progress;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Attempts.Tests.Progress;

/// <summary>SPEC §4.7: per class and per card, from the scored results of first attempts; per card only where the answer is revealed.</summary>
public sealed class ProgressCalculatorTests
{
    private static readonly CardClasses Catalogue = new(
        ["disproportion", "hygiene", "growth", "domain"],
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["secrets-in-repo"] = "hygiene",
            ["swallowed-error"] = "hygiene",
            ["god-class"] = "growth",
            ["money-in-float"] = "domain",
        },
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["disproportion"] = "Disproportion",
            ["hygiene"] = "Hygiene",
            ["growth"] = "Growth",
            ["domain"] = "Domain",
        },
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["secrets-in-repo"] = "Secrets in the repository",
            ["swallowed-error"] = "Swallowed error",
            ["god-class"] = "God class",
            ["money-in-float"] = "Money in float",
        });

    [Fact]
    public void EachCard_CountsMetFoundMissedPickedWhenAbsentAndTreatedRight()
    {
        var first = Score(
            new KeyFinding("secrets-in-repo", 3, ["auto.secrets"]),
            new KeyFinding("money-in-float", 3, ["manual.representation"]),
            new KeyFinding("swallowed-error", 2, ["manual.handle-errors"]),
            picks: [new("secrets-in-repo", ["auto.secrets"]), new("money-in-float", ["manual.split"]), new("god-class", ["manual.split"])]);
        var second = Score(
            new KeyFinding("god-class", 2, ["accept.fits-context"]),
            new KeyFinding("secrets-in-repo", 3, ["auto.secrets"]),
            picks: [new("god-class", ["accept.fits-context"])]);

        var progress = ProgressCalculator.Calculate([Revealed(first), Revealed(second)], Catalogue);

        Assert.Equal(2, progress.Tasks);
        Assert.Equal(
            [
                new CardProgress("secrets-in-repo", "Secrets in the repository", "hygiene", Met: 2, Found: 1, Missed: 1, PickedWhenAbsent: 0, TreatedRight: 1),
                new CardProgress("swallowed-error", "Swallowed error", "hygiene", Met: 1, Found: 0, Missed: 1, PickedWhenAbsent: 0, TreatedRight: 0),
                new CardProgress("god-class", "God class", "growth", Met: 1, Found: 1, Missed: 0, PickedWhenAbsent: 1, TreatedRight: 1),
                new CardProgress("money-in-float", "Money in float", "domain", Met: 1, Found: 1, Missed: 0, PickedWhenAbsent: 0, TreatedRight: 0),
            ],
            progress.Cards);
    }

    [Fact]
    public void EachClass_SumsItsCards_AndEveryClassIsListedInOrder()
    {
        var first = Score(
            new KeyFinding("secrets-in-repo", 3, ["auto.secrets"]),
            new KeyFinding("swallowed-error", 2, ["manual.handle-errors"]),
            picks: [new("secrets-in-repo", ["auto.secrets"]), new("swallowed-error", ["rule.conventions"]), new("money-in-float", ["manual.representation"])]);

        var progress = ProgressCalculator.Calculate([Revealed(first)], Catalogue);

        Assert.Equal(
            [
                new ClassProgress("disproportion", "Disproportion", 0, 0, 0),
                new ClassProgress("hygiene", "Hygiene", Met: 2, Found: 2, TreatedRight: 1),
                new ClassProgress("growth", "Growth", 0, 0, 0),
                // An extra pick is not a finding met: the class counts nothing for it.
                new ClassProgress("domain", "Domain", 0, 0, 0),
            ],
            progress.Classes);
    }

    [Fact]
    public void NoAttempts_IsAnEmptyProgress_WithEveryClassAtZero()
    {
        var progress = ProgressCalculator.Calculate([], Catalogue);

        Assert.Equal(0, progress.Tasks);
        Assert.Equal(0, progress.NotRevealed);
        Assert.Empty(progress.Cards);
        Assert.All(progress.Classes, @class => Assert.Equal(0, @class.Met));
    }

    [Fact]
    public void ACardContentDoesNotKnow_IsListedLast_NamedByItsSlug()
    {
        var first = Score(new KeyFinding("mystery", 1, ["manual.remove"]), new KeyFinding("money-in-float", 3, ["manual.representation"]), picks: []);

        var progress = ProgressCalculator.Calculate([Revealed(first)], Catalogue);

        Assert.Equal(["money-in-float", "mystery"], progress.Cards.Select(card => card.Card));
        Assert.Null(progress.Cards[1].Class);
        Assert.Equal("mystery", progress.Cards[1].Name);
    }

    [Fact]
    public void ATaskWhoseAnswerIsNotRevealed_NamesNoCard_ButCountsInItsClasses()
    {
        var hidden = Score(
            new KeyFinding("secrets-in-repo", 3, ["auto.secrets"]),
            new KeyFinding("money-in-float", 3, ["manual.representation"]),
            picks: [new("secrets-in-repo", ["auto.secrets"]), new("god-class", ["manual.split"])]);
        var seen = Score(
            new KeyFinding("swallowed-error", 2, ["manual.handle-errors"]),
            new KeyFinding("secrets-in-repo", 3, ["auto.secrets"]),
            picks: [new("swallowed-error", ["manual.handle-errors"])]);

        var progress = ProgressCalculator.Calculate([new FirstAttempt(hidden, AnswerRevealed: false), Revealed(seen)], Catalogue);

        Assert.Equal(2, progress.Tasks);
        Assert.Equal(1, progress.NotRevealed);

        // Only the revealed task's cards: neither the hidden key's money-in-float nor its extra pick, and
        // secrets-in-repo counted once, as the revealed task met it.
        Assert.Equal(
            [
                new CardProgress("secrets-in-repo", "Secrets in the repository", "hygiene", Met: 1, Found: 0, Missed: 1, PickedWhenAbsent: 0, TreatedRight: 0),
                new CardProgress("swallowed-error", "Swallowed error", "hygiene", Met: 1, Found: 1, Missed: 0, PickedWhenAbsent: 0, TreatedRight: 1),
            ],
            progress.Cards);

        // The classes name no card, so they count both first attempts.
        Assert.Equal(
            [
                new ClassProgress("disproportion", "Disproportion", 0, 0, 0),
                new ClassProgress("hygiene", "Hygiene", Met: 3, Found: 2, TreatedRight: 2),
                new ClassProgress("growth", "Growth", 0, 0, 0),
                new ClassProgress("domain", "Domain", Met: 1, Found: 0, TreatedRight: 0),
            ],
            progress.Classes);
    }

    [Fact]
    public void RevealingTheAnswer_ChangesOnlyWhichCardsAreListed()
    {
        var first = Score(
            new KeyFinding("secrets-in-repo", 3, ["auto.secrets"]),
            new KeyFinding("money-in-float", 3, ["manual.representation"]),
            picks: [new("secrets-in-repo", ["auto.secrets"])]);

        var before = ProgressCalculator.Calculate([new FirstAttempt(first, AnswerRevealed: false)], Catalogue);
        var after = ProgressCalculator.Calculate([Revealed(first)], Catalogue);

        Assert.Empty(before.Cards);
        Assert.Equal(["secrets-in-repo", "money-in-float"], after.Cards.Select(card => card.Card));
        Assert.Equal(before.Classes, after.Classes);
        Assert.Equal((1, 1), (before.Tasks, before.NotRevealed));
        Assert.Equal((1, 0), (after.Tasks, after.NotRevealed));
    }

    private static FirstAttempt Revealed(DiagnosisScore score) => new(score, AnswerRevealed: true);

    private static DiagnosisScore Score(KeyFinding first, KeyFinding second, PickedCard[] picks) =>
        DiagnosisScoring.Score(new DiagnosisAnswer(picks), [first, second], ScoringParameters.Default);

    private static DiagnosisScore Score(KeyFinding first, KeyFinding second, KeyFinding third, PickedCard[] picks) =>
        DiagnosisScoring.Score(new DiagnosisAnswer(picks), [first, second, third], ScoringParameters.Default);
}
