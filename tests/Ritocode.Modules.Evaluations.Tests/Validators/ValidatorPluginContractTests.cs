using System.Text.Json.Nodes;
using Ritocode.Modules.Evaluations.Sandbox;
using Ritocode.Modules.Evaluations.Validators;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Evaluations.Tests.Validators;

/// <summary>
/// The interface exercised end to end through the smallest plugin that means anything, and the command
/// parsing every command-running plugin shares.
/// </summary>
public sealed class ValidatorPluginContractTests
{
    private readonly ExitCodeValidator _plugin = new();

    [Fact]
    public void Plan_ReadsTheCommandFromWith_AsAnArgumentVector()
    {
        var step = Build.Step(with: new JsonObject { ["command"] = new JsonArray("dotnet", "test", "--no-build") });

        var plan = _plugin.Plan(step);

        Assert.True(plan.IsSuccess);
        Assert.Equal(["dotnet", "test", "--no-build"], plan.Value.Command);
    }

    [Fact]
    public void Plan_WithNoCommand_IsAValidationErrorNamingTheField()
    {
        var plan = _plugin.Plan(Build.Step(id: "unit-tests"));

        Assert.False(plan.IsSuccess);
        Assert.Equal(ErrorType.Validation, plan.Error.Type);
        Assert.NotNull(plan.Error.Fields);
        Assert.True(plan.Error.Fields.ContainsKey("validators.unit-tests.with.command"));
    }

    public static TheoryData<JsonNode> CommandsThatCannotRun => new()
    {
        new JsonArray(),
        JsonValue.Create("dotnet test")!,
        new JsonArray("dotnet", ""),
        new JsonArray("dotnet", "   "),
        new JsonArray("dotnet", 3),
        new JsonArray("dotnet", new JsonArray("test")),
    };

    [Theory]
    [MemberData(nameof(CommandsThatCannotRun))]
    public void Plan_RefusesACommandThatIsNotANonEmptyListOfNonBlankStrings(JsonNode command)
    {
        // A single string is refused on purpose: splitting it would take a shell, a second interpreter of
        // content with rules the author did not write the command against.
        var plan = ValidatorRunPlan.FromCommand(Build.Step(with: new JsonObject { ["command"] = command.DeepClone() }));

        Assert.False(plan.IsSuccess);
    }

    [Fact]
    public async Task Interpret_ACompletedRun_IsJudgedByThePlugin()
    {
        var step = Build.Step();

        var passed = await _plugin.InterpretAsync(step, Build.Run(exitCode: 0), TestContext.Current.CancellationToken);
        var failed = await _plugin.InterpretAsync(step, Build.Run(exitCode: 1), TestContext.Current.CancellationToken);

        Assert.Equal(ValidatorOutcome.Passed, passed.Outcome);
        Assert.Equal(ValidatorOutcome.Failed, failed.Outcome);
    }

    [Theory]
    [InlineData(SandboxRunOutcome.TimedOut)]
    [InlineData(SandboxRunOutcome.ResourceExhausted)]
    [InlineData(SandboxRunOutcome.Crashed)]
    public async Task Interpret_ARunThatDidNotComplete_IsNeverAPassOrAFail(SandboxRunOutcome outcome)
    {
        // Exit code 0 on a killed container is exactly the case a careless plugin would call a pass.
        var result = await _plugin.InterpretAsync(Build.Step(), Build.Run(outcome, exitCode: 0), TestContext.Current.CancellationToken);

        Assert.Equal(ValidatorOutcome.NotCompleted, result.Outcome);
        Assert.Equal(outcome, result.RunOutcome);
    }

    [Fact]
    public void ThePlugin_RegistersUnderItsType()
    {
        var registry = new ValidatorPluginRegistry([_plugin]);

        Assert.Same(_plugin, registry.Find("exit-code"));
    }
}
