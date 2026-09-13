using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;

namespace Ritocode.Modules.Evaluations.Tests.Validators;

/// <summary>The result schema, asserted as literal JSON: this string is what #38 compares between two runs.</summary>
public sealed class ValidatorResultsTests
{
    [Fact]
    public void ToJson_WritesEveryField_InAFixedOrder_WithEnumsAsCamelCaseNames()
    {
        var compile = Build.Step(id: "compile", type: "compile");
        var tests = Build.Step(id: "unit-tests", type: "test");
        var lint = Build.Step(id: "lint", type: "lint");

        var json = ValidatorResults.ToJson(
        [
            ValidatorResult.Judged(compile, Build.Run(), passed: true),
            ValidatorResult.NotCompleted(tests, Build.Run(SandboxRunOutcome.TimedOut, 137), "Killed after 120 s."),
            ValidatorResult.Skipped(lint),
        ]);

        Assert.Equal(
            """{"schemaVersion":1,"validators":[{"id":"compile","type":"compile","outcome":"passed","runOutcome":"completed","summary":null,"checks":[]},{"id":"unit-tests","type":"test","outcome":"notCompleted","runOutcome":"timedOut","summary":"Killed after 120 s.","checks":[]},{"id":"lint","type":"lint","outcome":"skipped","runOutcome":null,"summary":null,"checks":[]}]}""",
            json);
    }

    [Fact]
    public void ToJson_WritesChecksSortedOrdinally_WithTheirOutcomes()
    {
        var json = ValidatorResults.ToJson(
        [
            ValidatorResult.Judged(
                Build.Step(id: "unit-tests", type: "test"),
                Build.Run(exitCode: 1),
                passed: false,
                [
                    new ValidatorCheck("Splits_ThreeWays", ValidatorCheckOutcome.Failed),
                    new ValidatorCheck("Rounds_Down", ValidatorCheckOutcome.Passed),
                    new ValidatorCheck("Handles_Zero", ValidatorCheckOutcome.Skipped),
                ],
                "1 of 3 tests failed."),
        ]);

        Assert.Equal(
            """{"schemaVersion":1,"validators":[{"id":"unit-tests","type":"test","outcome":"failed","runOutcome":"completed","summary":"1 of 3 tests failed.","checks":[{"name":"Handles_Zero","outcome":"skipped"},{"name":"Rounds_Down","outcome":"passed"},{"name":"Splits_ThreeWays","outcome":"failed"}]}]}""",
            json);
    }

    [Fact]
    public void ToJson_IsByteIdentical_ForTheSameResultsAssembledInAnotherOrder()
    {
        // The determinism claim in miniature: a test runner reports tests in whatever order it ran them.
        var step = Build.Step(id: "unit-tests", type: "test");
        ValidatorCheck[] found =
        [
            new("B_Second", ValidatorCheckOutcome.Passed),
            new("A_First", ValidatorCheckOutcome.Failed),
            new("C_Third", ValidatorCheckOutcome.Passed),
        ];

        var once = ValidatorResults.ToJson([ValidatorResult.Judged(step, Build.Run(), passed: false, found)]);
        var again = ValidatorResults.ToJson([ValidatorResult.Judged(step, Build.Run(), passed: false, found.Reverse())]);

        Assert.Equal(once, again);
    }

    [Fact]
    public void ToJson_LeavesOutEverythingThatDiffersBetweenTwoRuns()
    {
        // Two runs of one submission differ in duration and in their raw output; the report must not.
        var step = Build.Step();
        var fast = Build.Run() with { Duration = TimeSpan.FromSeconds(2), Stdout = new CapturedOutput("run 1", Truncated: false) };
        var slow = Build.Run() with { Duration = TimeSpan.FromSeconds(9), Stdout = new CapturedOutput("run 2", Truncated: true) };

        Assert.Equal(
            ValidatorResults.ToJson([ValidatorResult.Judged(step, fast, passed: true)]),
            ValidatorResults.ToJson([ValidatorResult.Judged(step, slow, passed: true)]));
    }

    [Fact]
    public void ToJson_WritesANotRunnableStep_WithItsReasonAndNoRunOutcome()
    {
        // A summary with no character the default encoder escapes, so the literal below reads as written.
        var json = ValidatorResults.ToJson([ValidatorResult.NotRunnable(Build.Step(id: "lint", type: "lint"), "No validator answers the type lint.")]);

        Assert.Equal(
            """{"schemaVersion":1,"validators":[{"id":"lint","type":"lint","outcome":"notRunnable","runOutcome":null,"summary":"No validator answers the type lint.","checks":[]}]}""",
            json);
    }

    [Fact]
    public void ToJson_OfNoResults_IsAnEmptyPipeline()
    {
        Assert.Equal("""{"schemaVersion":1,"validators":[]}""", ValidatorResults.ToJson([]));
    }
}
