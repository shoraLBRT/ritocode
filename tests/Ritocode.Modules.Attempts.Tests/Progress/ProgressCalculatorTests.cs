using Ritocode.Modules.Attempts.Progress;
using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Tests.Progress;

/// <summary>SPEC §4.7: per class and per card, from the scored results of first attempts.</summary>
public sealed class ProgressCalculatorTests
{
    private static readonly string[] Classes = ["disproportion", "hygiene", "growth", "domain"];

    private static readonly Dictionary<string, string> ClassOf = new(StringComparer.Ordinal)
    {
        ["secrets-in-repo"] = "hygiene",
        ["swallowed-error"] = "hygiene",
        ["god-class"] = "growth",
        ["money-in-float"] = "domain",
    };

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

        var progress = ProgressCalculator.Calculate([first, second], Classes, ClassOf);

        Assert.Equal(2, progress.Tasks);
        Assert.Equal(
            [
                new CardProgress("secrets-in-repo", "hygiene", Met: 2, Found: 1, Missed: 1, PickedWhenAbsent: 0, TreatedRight: 1),
                new CardProgress("swallowed-error", "hygiene", Met: 1, Found: 0, Missed: 1, PickedWhenAbsent: 0, TreatedRight: 0),
                new CardProgress("god-class", "growth", Met: 1, Found: 1, Missed: 0, PickedWhenAbsent: 1, TreatedRight: 1),
                new CardProgress("money-in-float", "domain", Met: 1, Found: 1, Missed: 0, PickedWhenAbsent: 0, TreatedRight: 0),
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

        var progress = ProgressCalculator.Calculate([first], Classes, ClassOf);

        Assert.Equal(
            [
                new ClassProgress("disproportion", 0, 0, 0),
                new ClassProgress("hygiene", Met: 2, Found: 2, TreatedRight: 1),
                new ClassProgress("growth", 0, 0, 0),
                // An extra pick is not a finding met: the class counts nothing for it.
                new ClassProgress("domain", 0, 0, 0),
            ],
            progress.Classes);
    }

    [Fact]
    public void NoAttempts_IsAnEmptyProgress_WithEveryClassAtZero()
    {
        var progress = ProgressCalculator.Calculate([], Classes, ClassOf);

        Assert.Equal(0, progress.Tasks);
        Assert.Empty(progress.Cards);
        Assert.All(progress.Classes, @class => Assert.Equal(0, @class.Met));
    }

    [Fact]
    public void ACardOfNoKnownClass_IsListedLast()
    {
        var first = Score(new KeyFinding("mystery", 1, ["manual.remove"]), new KeyFinding("money-in-float", 3, ["manual.representation"]), picks: []);

        var progress = ProgressCalculator.Calculate([first], Classes, ClassOf);

        Assert.Equal(["money-in-float", "mystery"], progress.Cards.Select(card => card.Card));
        Assert.Null(progress.Cards[1].Class);
    }

    private static DiagnosisScore Score(KeyFinding first, KeyFinding second, PickedCard[] picks) =>
        DiagnosisScoring.Score(new DiagnosisAnswer(picks), [first, second], ScoringParameters.Default);

    private static DiagnosisScore Score(KeyFinding first, KeyFinding second, KeyFinding third, PickedCard[] picks) =>
        DiagnosisScoring.Score(new DiagnosisAnswer(picks), [first, second, third], ScoringParameters.Default);
}
