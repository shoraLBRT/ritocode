using System.Text.Json;
using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Tests.Scoring;

/// <summary>The rules of docs/SPEC.md §5.2, starting with the worked example of §5.3.</summary>
public sealed class DiagnosisScoringTests
{
    // SPEC §5.3: secrets (weight 3), a god class (2) and money in a float (3).
    private static readonly KeyFinding[] WorkedKey =
    [
        new("secrets-in-repo", 3, ["auto.secrets"]),
        new("god-class", 2, ["accept.fits-context"]),
        new("money-in-float", 3, ["manual.representation"]),
    ];

    private static readonly DiagnosisAnswer WorkedAnswer = new(
    [
        new("secrets-in-repo", ["auto.secrets", "manual.extract-config"]),
        new("god-class", ["manual.split"]),
        new("magic-numbers", ["manual.extract-config"]),
    ]);

    [Fact]
    public void TheWorkedExample_Scores49Of120()
    {
        var score = DiagnosisScoring.Score(WorkedAnswer, WorkedKey, ScoringParameters.Default);

        Assert.Equal(49, score.Total);
        Assert.Equal(120, score.Maximum);
        Assert.False(score.IsCorrect);

        Assert.Equal(
            [
                ("secrets-in-repo", CardOutcome.Found, 43),
                ("god-class", CardOutcome.Found, 18),
                ("money-in-float", CardOutcome.Missed, -9),
                ("magic-numbers", CardOutcome.Extra, -3),
            ],
            score.Cards.Select(line => (line.Card, line.Outcome, line.Points)));

        var secrets = score.Cards[0].Treatment!;
        Assert.True(secrets.Matched);
        Assert.Equal(["auto.secrets"], secrets.MatchedLeaves);
        Assert.Equal(["manual.extract-config"], secrets.WrongLeaves);

        var godClass = score.Cards[1].Treatment!;
        Assert.False(godClass.Matched);
        Assert.Equal(["manual.split"], godClass.WrongLeaves);
        Assert.Equal(["accept.fits-context"], godClass.KeyLeaves);

        Assert.Null(score.Cards[2].Treatment);
        Assert.Null(score.Cards[3].Treatment);
    }

    [Fact]
    public void ACleanTask_WithNothingPicked_Scores0Of0_AndIsCorrect()
    {
        var score = DiagnosisScoring.Score(new DiagnosisAnswer([]), [], ScoringParameters.Default);

        Assert.Equal(0, score.Total);
        Assert.Equal(0, score.Maximum);
        Assert.True(score.IsCorrect);
        Assert.Empty(score.Cards);
    }

    [Fact]
    public void ACleanTask_WithAPick_Scores0Of0_AndIsNotCorrect()
    {
        var score = DiagnosisScoring.Score(
            new DiagnosisAnswer([new("god-class", ["manual.split"])]),
            [],
            ScoringParameters.Default);

        Assert.Equal(0, score.Total);
        Assert.Equal(0, score.Maximum);
        Assert.False(score.IsCorrect);
        Assert.Equal(CardOutcome.Extra, Assert.Single(score.Cards).Outcome);
    }

    [Fact]
    public void TheScore_DoesNotDependOnTheOrderOfPicksOrLeaves()
    {
        var reordered = new DiagnosisAnswer(
        [
            new("magic-numbers", ["manual.extract-config"]),
            new("god-class", ["manual.split"]),
            new("secrets-in-repo", ["manual.extract-config", "auto.secrets"]),
        ]);
        var key = WorkedKey.Select(finding => finding with { Leaves = [.. finding.Leaves.Reverse()] }).ToArray();

        var once = DiagnosisScoring.Score(WorkedAnswer, WorkedKey, ScoringParameters.Default);
        var again = DiagnosisScoring.Score(reordered, key, ScoringParameters.Default);

        Assert.Equal(JsonSerializer.Serialize(once), JsonSerializer.Serialize(again));
    }

    [Fact]
    public void AnAnswerThatMatchesTheKey_ScoresTheMaximum_AndIsCorrect()
    {
        var answer = new DiagnosisAnswer([.. WorkedKey.Select(finding => new PickedCard(finding.Card, [finding.Leaves[0]]))]);

        var score = DiagnosisScoring.Score(answer, WorkedKey, ScoringParameters.Default);

        Assert.Equal(120, score.Total);
        Assert.Equal(120, score.Maximum);
        Assert.True(score.IsCorrect);
    }

    [Fact]
    public void AnyOneLeafOfTheKey_IsEnough_AndCountsOnce()
    {
        var key = new KeyFinding[] { new("secrets-in-repo", 3, ["auto.secrets", "rule.conventions", "manual.extract-config"]) };

        var one = DiagnosisScoring.Score(new DiagnosisAnswer([new("secrets-in-repo", ["rule.conventions"])]), key, ScoringParameters.Default);
        var all = DiagnosisScoring.Score(new DiagnosisAnswer([new("secrets-in-repo", ["auto.secrets", "rule.conventions", "manual.extract-config"])]), key, ScoringParameters.Default);

        Assert.Equal(45, one.Total);
        Assert.Equal(45, all.Total);
        Assert.True(one.IsCorrect);
    }

    [Fact]
    public void ALeafTickedTwice_CountsOnce()
    {
        var key = new KeyFinding[] { new("god-class", 2, ["accept.fits-context"]) };
        var answer = new DiagnosisAnswer([new("god-class", ["manual.split", "manual.split"])]);

        var line = Assert.Single(DiagnosisScoring.Score(answer, key, ScoringParameters.Default).Cards);

        Assert.Equal(18, line.Points);
        Assert.Equal(["manual.split"], line.Treatment!.WrongLeaves);
    }

    [Fact]
    public void TheLeavesOfAnExtraCard_AreNotScored()
    {
        var key = Array.Empty<KeyFinding>();
        var bare = DiagnosisScoring.Score(new DiagnosisAnswer([new("god-class", [])]), key, ScoringParameters.Default);
        var ticked = DiagnosisScoring.Score(new DiagnosisAnswer([new("god-class", ["manual.split", "rule.limits", "auto.metrics"])]), key, ScoringParameters.Default);

        Assert.Equal(-3, Assert.Single(bare.Cards).Points);
        Assert.Equal(-3, Assert.Single(ticked.Cards).Points);
    }

    [Fact]
    public void TheTotal_IsFlooredAtZero()
    {
        var score = DiagnosisScoring.Score(new DiagnosisAnswer([new("magic-numbers", [])]), WorkedKey, ScoringParameters.Default);

        Assert.Equal(-27, score.Cards.Sum(line => line.Points));
        Assert.Equal(0, score.Total);
        Assert.Equal(120, score.Maximum);
    }

    [Fact]
    public void TheParameters_AreWhatTheScoreIsComputedWith()
    {
        var harsh = new ScoringParameters { Found = 10, Missed = 5, Extra = 4, TreatmentMatched = 5, WrongLeaf = 1 };

        var score = DiagnosisScoring.Score(WorkedAnswer, WorkedKey, harsh);

        // Secrets 30 + 15 − 1; god class 20 − 1; money −15; magic numbers −4.
        Assert.Equal(44 + 19 - 15 - 4, score.Total);
        Assert.Equal(120, score.Maximum);
    }

    [Fact]
    public void ACardPickedTwice_OrListedTwiceInTheKey_IsRejected()
    {
        var twice = new DiagnosisAnswer([new("god-class", []), new("god-class", ["manual.split"])]);

        Assert.Throws<ArgumentException>(() => DiagnosisScoring.Score(twice, WorkedKey, ScoringParameters.Default));
        Assert.Throws<ArgumentException>(() => DiagnosisScoring.Score(WorkedAnswer, [WorkedKey[0], WorkedKey[0]], ScoringParameters.Default));
    }

    [Fact]
    public void AKeyCardWithoutAPositiveWeight_IsRejected()
    {
        var key = new KeyFinding[] { new("god-class", 0, ["manual.split"]) };

        Assert.Throws<ArgumentOutOfRangeException>(() => DiagnosisScoring.Score(new DiagnosisAnswer([]), key, ScoringParameters.Default));
    }
}
