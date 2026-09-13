using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;

namespace Ritocode.Modules.Evaluations.Tests.Validators;

public sealed class ValidatorResultTests
{
    [Fact]
    public void Judged_ACompletedRun_CarriesTheVerdictAndTheStep()
    {
        var step = Build.Step(id: "unit-tests", type: "test");

        var passed = ValidatorResult.Judged(step, Build.Run(), passed: true, summary: "12 of 12 tests passed.");
        var failed = ValidatorResult.Judged(step, Build.Run(exitCode: 1), passed: false);

        Assert.Equal(ValidatorOutcome.Passed, passed.Outcome);
        Assert.Equal(ValidatorOutcome.Failed, failed.Outcome);
        Assert.Equal("unit-tests", passed.Id);
        Assert.Equal("test", passed.Type);
        Assert.Equal(SandboxRunOutcome.Completed, passed.RunOutcome);
        Assert.Equal("12 of 12 tests passed.", passed.Summary);
    }

    [Fact]
    public void Judged_OrdersTheChecksOrdinally_WhateverOrderTheyWereFoundIn()
    {
        var result = ValidatorResult.Judged(
            Build.Step(),
            Build.Run(),
            passed: false,
            [
                new ValidatorCheck("Splits_ThreeWays", ValidatorCheckOutcome.Failed),
                new ValidatorCheck("Rounds_Down", ValidatorCheckOutcome.Passed),
                new ValidatorCheck("rounds_up", ValidatorCheckOutcome.Skipped),
            ]);

        Assert.Equal(["Rounds_Down", "Splits_ThreeWays", "rounds_up"], result.Checks.Select(check => check.Name));
    }

    [Fact]
    public void Judged_RefusesTwoChecksUnderOneName()
    {
        // Two outcomes under one name would make the projection depend on which one was kept.
        Assert.Throws<ArgumentException>(() => ValidatorResult.Judged(
            Build.Step(),
            Build.Run(),
            passed: true,
            [new ValidatorCheck("Rounds_Down", ValidatorCheckOutcome.Passed), new ValidatorCheck("Rounds_Down", ValidatorCheckOutcome.Failed)]));
    }

    [Theory]
    [InlineData(SandboxRunOutcome.TimedOut)]
    [InlineData(SandboxRunOutcome.ResourceExhausted)]
    [InlineData(SandboxRunOutcome.Crashed)]
    public void Judged_RefusesARunThatDidNotComplete(SandboxRunOutcome outcome)
    {
        // A killed container's exit code is not the validator's answer, so no verdict can come from it.
        Assert.Throws<ArgumentException>(() => ValidatorResult.Judged(Build.Step(), Build.Run(outcome, exitCode: 137), passed: false));
    }

    [Theory]
    [InlineData(SandboxRunOutcome.TimedOut)]
    [InlineData(SandboxRunOutcome.ResourceExhausted)]
    [InlineData(SandboxRunOutcome.Crashed)]
    public void NotCompleted_KeepsHowTheRunEnded_AndJudgesNothing(SandboxRunOutcome outcome)
    {
        var result = ValidatorResult.NotCompleted(Build.Step(), Build.Run(outcome, exitCode: 137), "Killed after 120 s.");

        Assert.Equal(ValidatorOutcome.NotCompleted, result.Outcome);
        Assert.Equal(outcome, result.RunOutcome);
        Assert.Empty(result.Checks);
        Assert.Equal("Killed after 120 s.", result.Summary);
    }

    [Fact]
    public void NotCompleted_RefusesARunThatCompleted()
    {
        Assert.Throws<ArgumentException>(() => ValidatorResult.NotCompleted(Build.Step(), Build.Run()));
    }

    [Fact]
    public void NotRunnable_HasNoRunOutcome_AndSaysWhy()
    {
        var result = ValidatorResult.NotRunnable(Build.Step(id: "lint", type: "lint"), "No validator answers the type 'lint'.");

        Assert.Equal(ValidatorOutcome.NotRunnable, result.Outcome);
        Assert.Null(result.RunOutcome);
        Assert.Empty(result.Checks);
        Assert.Equal("No validator answers the type 'lint'.", result.Summary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NotRunnable_WithoutAReason_IsRefused(string summary)
    {
        // A content fault nobody can read is a report that blames the attempt by default.
        Assert.Throws<ArgumentException>(() => ValidatorResult.NotRunnable(Build.Step(), summary));
    }

    [Fact]
    public void Skipped_HasNoRunOutcome_NoChecks_AndNoSummary()
    {
        var result = ValidatorResult.Skipped(Build.Step(id: "unit-tests", type: "test"));

        Assert.Equal(ValidatorOutcome.Skipped, result.Outcome);
        Assert.Null(result.RunOutcome);
        Assert.Empty(result.Checks);
        Assert.Null(result.Summary);
        Assert.Equal("unit-tests", result.Id);
    }
}
