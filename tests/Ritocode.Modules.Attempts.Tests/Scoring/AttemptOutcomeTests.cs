using Ritocode.Modules.Attempts.Scoring;

namespace Ritocode.Modules.Attempts.Tests.Scoring;

/// <summary>
/// What a result says without the answer, and the four statuses of a task (docs/SPEC.md §4.3, §4.5,
/// ADR 0017), by the maintainer's rules of 2026-11-12 (#229).
/// </summary>
public sealed class AttemptOutcomeTests
{
    private static readonly KeyFinding[] Key =
    [
        new("secrets-in-repo", 3, ["auto.secrets", "rule.conventions"]),
        new("money-in-float", 3, ["manual.representation"]),
    ];

    private static readonly DateTimeOffset Monday = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnAnswer_CountsWhatWasFound_TreatedMissedAndExtra_AndIsNotSolved()
    {
        var score = Score(
            new("secrets-in-repo", ["auto.secrets"]),
            new("magic-numbers", ["manual.extract-config"]));

        // Secrets +45, money missed −9, an extra pick −3.
        Assert.Equal(new AttemptOutcome(33, 90, Present: 2, Found: 1, Treated: 1, Missed: 1, Extra: 1, Solved: false), AttemptOutcome.Of(score));
    }

    [Fact]
    public void EveryFindingFound_WithARightLeaf_AndNoExtraPick_Solves_EvenWithAWrongLeafBesideIt()
    {
        var outcome = AttemptOutcome.Of(Score(
            new("secrets-in-repo", ["auto.secrets", "manual.extract-config"]),
            new("money-in-float", ["manual.representation"])));

        Assert.True(outcome.Solved);
        Assert.Equal(2, outcome.Treated);
        Assert.True(outcome.Total < outcome.Maximum);
    }

    [Fact]
    public void AnExtraPick_OrAFindingWithoutARightLeaf_OrAMiss_DoesNotSolve()
    {
        Assert.False(AttemptOutcome.Of(Score(
            new("secrets-in-repo", ["auto.secrets"]),
            new("money-in-float", ["manual.representation"]),
            new("magic-numbers", ["manual.extract-config"]))).Solved);

        Assert.False(AttemptOutcome.Of(Score(
            new("secrets-in-repo", ["auto.secrets"]),
            new("money-in-float", ["manual.split"]))).Solved);

        Assert.False(AttemptOutcome.Of(Score(new PickedCard("secrets-in-repo", ["auto.secrets"]))).Solved);
    }

    [Fact]
    public void OnACleanTask_NothingPicked_Solves_AndAnyPickDoesNot()
    {
        var nothing = AttemptOutcome.Of(DiagnosisScoring.Score(new([]), [], ScoringParameters.Default));
        var something = AttemptOutcome.Of(DiagnosisScoring.Score(new([new("magic-numbers", ["manual.extract-config"])]), [], ScoringParameters.Default));

        Assert.Equal(new AttemptOutcome(0, 0, 0, 0, 0, 0, 0, Solved: true), nothing);
        Assert.False(something.Solved);
        Assert.Equal(1, something.Extra);
    }

    [Fact]
    public void NoSubmittedAttempt_IsNew()
    {
        Assert.Equal(LearnerTaskStatus.New, TaskStatuses.Of([], revealedAt: null));
    }

    [Fact]
    public void SubmittedWithoutSolving_IsTried_WhateverTheScore()
    {
        Assert.Equal(LearnerTaskStatus.Tried, TaskStatuses.Of([new(Monday, Solved: false), new(Monday.AddHours(1), Solved: false)], revealedAt: null));
    }

    [Fact]
    public void ASolvingAttempt_IsSolved()
    {
        Assert.Equal(LearnerTaskStatus.Solved, TaskStatuses.Of([new(Monday, Solved: false), new(Monday.AddHours(1), Solved: true)], revealedAt: null));
    }

    [Fact]
    public void Tried_ThenRevealed_IsGaveUp()
    {
        Assert.Equal(LearnerTaskStatus.GaveUp, TaskStatuses.Of([new(Monday, Solved: false)], revealedAt: Monday.AddMinutes(5)));
    }

    [Fact]
    public void Solved_ThenRevealed_StaysSolved()
    {
        Assert.Equal(LearnerTaskStatus.Solved, TaskStatuses.Of([new(Monday, Solved: true)], revealedAt: Monday.AddMinutes(5)));
    }

    [Fact]
    public void Revealed_ThenSolvedInPractice_StaysGaveUp()
    {
        Assert.Equal(
            LearnerTaskStatus.GaveUp,
            TaskStatuses.Of([new(Monday, Solved: false), new(Monday.AddHours(1), Solved: true)], revealedAt: Monday.AddMinutes(5)));
    }

    private static DiagnosisScore Score(params PickedCard[] picks) =>
        DiagnosisScoring.Score(new DiagnosisAnswer(picks), Key, ScoringParameters.Default);
}
