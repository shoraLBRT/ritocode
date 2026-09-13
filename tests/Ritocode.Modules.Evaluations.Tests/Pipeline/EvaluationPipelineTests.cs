using System.Text.Json.Nodes;
using Ritocode.Modules.Evaluations.Pipeline;
using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Evaluations.Tests.Pipeline;

/// <summary>
/// The orchestration rules, over a runner that replays scripted observations. Nothing here starts a
/// container — nothing in the pipeline may — so every execution the pipeline asks for is visible as a
/// recorded request.
/// </summary>
public sealed class EvaluationPipelineTests
{
    private const string Workspace = "/evaluations/input";
    private const string Output = "/evaluations/output";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryStep_RunsInPipelineOrder_ThroughTheRunner_AndThePipelineRunsToTheEnd()
    {
        var runner = new ScriptedRunner();

        var outcome = await Pipeline(runner).RunAsync([Step("compile"), Step("unit-tests", timeoutSeconds: 300)], Workspace, Output, Token);

        Assert.True(outcome.RanToEnd);
        Assert.Equal(["compile", "unit-tests"], outcome.Results.Select(result => result.Id));
        Assert.All(outcome.Results, result => Assert.Equal(ValidatorOutcome.Passed, result.Outcome));

        Assert.Equal(["compile", "unit-tests"], runner.Requests.Select(request => request.StepId));
        Assert.Equal(["run", "compile"], runner.Requests[0].Command);
        Assert.Equal(TimeSpan.FromSeconds(300), runner.Requests[1].Timeout);
        Assert.All(runner.Requests, request =>
        {
            Assert.Equal(Workspace, request.WorkspaceDirectory);
            Assert.Equal(Output, request.OutputDirectory);
        });
    }

    [Fact]
    public async Task AFailedRequiredStep_StopsThePipeline_SkipsTheRest_AndTheAttemptStillRanToTheEnd()
    {
        // A task that does not compile has nothing to test — and a wrong answer is a score, not a broken run.
        var runner = new ScriptedRunner().Returns("compile", Build.Run(exitCode: 1));

        var outcome = await Pipeline(runner).RunAsync([Step("compile"), Step("unit-tests"), Step("style")], Workspace, Output, Token);

        Assert.True(outcome.RanToEnd);
        Assert.Equal(
            [ValidatorOutcome.Failed, ValidatorOutcome.Skipped, ValidatorOutcome.Skipped],
            outcome.Results.Select(result => result.Outcome));
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task AFailedOptionalStep_DoesNotStopThePipeline()
    {
        var runner = new ScriptedRunner().Returns("style", Build.Run(exitCode: 1));

        var outcome = await Pipeline(runner).RunAsync([Step("style", required: false), Step("unit-tests")], Workspace, Output, Token);

        Assert.True(outcome.RanToEnd);
        Assert.Equal([ValidatorOutcome.Failed, ValidatorOutcome.Passed], outcome.Results.Select(result => result.Outcome));
        Assert.Equal(2, runner.Requests.Count);
    }

    [Theory]
    [InlineData(SandboxRunOutcome.TimedOut)]
    [InlineData(SandboxRunOutcome.ResourceExhausted)]
    [InlineData(SandboxRunOutcome.Crashed)]
    public async Task AStepThatDidNotComplete_StopsThePipeline_AndTheAttemptDidNotRunToTheEnd(SandboxRunOutcome runOutcome)
    {
        // Optional or not: an attempt with a step that could not finish cannot be graded (ADR 0009 §4).
        var runner = new ScriptedRunner().Returns("style", Build.Run(runOutcome, exitCode: 137));

        var outcome = await Pipeline(runner).RunAsync([Step("style", required: false), Step("unit-tests")], Workspace, Output, Token);

        Assert.False(outcome.RanToEnd);
        Assert.Equal(ValidatorOutcome.NotCompleted, outcome.Results[0].Outcome);
        Assert.Equal(runOutcome, outcome.Results[0].RunOutcome);
        Assert.Equal(ValidatorOutcome.Skipped, outcome.Results[1].Outcome);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task AStepNoPluginAnswers_IsNotRunnable_AndNothingIsRunForIt()
    {
        var runner = new ScriptedRunner();

        var outcome = await Pipeline(runner).RunAsync([Step("lint", type: "lint"), Step("unit-tests")], Workspace, Output, Token);

        Assert.False(outcome.RanToEnd);
        Assert.Equal(ValidatorOutcome.NotRunnable, outcome.Results[0].Outcome);
        Assert.Null(outcome.Results[0].RunOutcome);
        Assert.Contains("'lint'", outcome.Results[0].Summary, StringComparison.Ordinal);
        Assert.Equal(ValidatorOutcome.Skipped, outcome.Results[1].Outcome);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task AStepWhoseWithCannotBePlanned_IsNotRunnable_NamingTheField()
    {
        var runner = new ScriptedRunner();
        var unplannable = Build.Step(id: "compile", type: "exit-code", with: new JsonObject());

        var outcome = await Pipeline(runner).RunAsync([unplannable], Workspace, Output, Token);

        Assert.False(outcome.RanToEnd);
        Assert.Equal(ValidatorOutcome.NotRunnable, outcome.Results[0].Outcome);
        Assert.Contains("validators.compile.with.command", outcome.Results[0].Summary, StringComparison.Ordinal);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task APluginReportingForAnotherStep_FailsTheEvaluationLoudly()
    {
        var forging = new ForgingPlugin((_, run) => ValidatorResult.Judged(Build.Step(id: "someone-else", type: "forging"), run, passed: true));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(new ScriptedRunner(), forging).RunAsync([Step("compile", type: "forging")], Workspace, Output, Token));
    }

    [Fact]
    public async Task APluginReportingARunOutcomeTheRunnerDidNotObserve_FailsTheEvaluationLoudly()
    {
        // The runner killed the container; the plugin judges a completed run it was never handed.
        var forging = new ForgingPlugin((step, _) => ValidatorResult.Judged(step, Build.Run(), passed: true));
        var runner = new ScriptedRunner().Returns("compile", Build.Run(SandboxRunOutcome.TimedOut, exitCode: 137));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(runner, forging).RunAsync([Step("compile", type: "forging")], Workspace, Output, Token));
    }

    [Fact]
    public async Task APluginReportingAStepThatRanAsSkipped_FailsTheEvaluationLoudly()
    {
        var forging = new ForgingPlugin((step, _) => ValidatorResult.Skipped(step));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(new ScriptedRunner(), forging).RunAsync([Step("compile", type: "forging")], Workspace, Output, Token));
    }

    [Fact]
    public async Task AnEmptyPipeline_IsRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Pipeline(new ScriptedRunner()).RunAsync([], Workspace, Output, Token));
    }

    [Fact]
    public async Task TheSameInput_EvaluatedTwice_ProducesByteIdenticalResults()
    {
        // The determinism claim at the orchestrator: two runs differ in duration and raw output, and the
        // results the report stores must not.
        ValidatorStepDefinition[] steps = [Step("compile"), Step("unit-tests"), Step("style", required: false)];

        var first = new ScriptedRunner()
            .Returns("unit-tests", Build.Run(exitCode: 1) with { Duration = TimeSpan.FromSeconds(4), Stdout = new CapturedOutput("first", false) });
        var second = new ScriptedRunner()
            .Returns("unit-tests", Build.Run(exitCode: 1) with { Duration = TimeSpan.FromSeconds(11), Stdout = new CapturedOutput("second", true) });

        var once = await Pipeline(first).RunAsync(steps, Workspace, Output, Token);
        var again = await Pipeline(second).RunAsync(steps, Workspace, Output, Token);

        Assert.Equal(ValidatorResults.ToJson(once.Results), ValidatorResults.ToJson(again.Results));
        Assert.Equal(once.RanToEnd, again.RanToEnd);
    }

    private static EvaluationPipeline Pipeline(ISandboxRunner runner, params IValidatorPlugin[] extra) =>
        new(new ValidatorPluginRegistry([new ExitCodeValidator(), .. extra]), runner);

    private static ValidatorStepDefinition Step(string id, string type = "exit-code", bool required = true, int timeoutSeconds = 120) =>
        new(id, type, Weight: 50, required, timeoutSeconds, new JsonObject { ["command"] = new JsonArray("run", id) });

    /// <summary>Replays a scripted observation per step — a completed, zero-exit run unless told otherwise — and records every request.</summary>
    private sealed class ScriptedRunner : ISandboxRunner
    {
        private readonly Dictionary<string, SandboxRunResult> _runs = new(StringComparer.Ordinal);

        public List<SandboxRunRequest> Requests { get; } = [];

        public ScriptedRunner Returns(string stepId, SandboxRunResult run)
        {
            _runs[stepId] = run;
            return this;
        }

        public Task<SandboxRunResult> RunAsync(SandboxRunRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_runs.TryGetValue(request.StepId, out var run) ? run : Build.Run());
        }
    }

    /// <summary>A plugin that reports whatever it is told to, to prove the pipeline does not believe a false report.</summary>
    private sealed class ForgingPlugin(Func<ValidatorStepDefinition, SandboxRunResult, ValidatorResult> interpret) : IValidatorPlugin
    {
        public string Type => "forging";

        public Result<ValidatorRunPlan> Plan(ValidatorStepDefinition step) => ValidatorRunPlan.FromCommand(step);

        public Task<ValidatorResult> InterpretAsync(ValidatorStepDefinition step, SandboxRunResult run, CancellationToken cancellationToken) =>
            Task.FromResult(interpret(step, run));
    }
}
