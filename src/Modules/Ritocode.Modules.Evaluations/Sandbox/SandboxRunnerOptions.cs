using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Evaluations.Sandbox;

/// <summary>How the worker host reaches Docker.</summary>
/// <remarks>
/// Nothing about the container is configurable here: the containment flags are the runner's constants
/// (ADR 0006 §1), and the image and its limits belong to the environment a run is handed (§2, §6).
/// </remarks>
public sealed class SandboxRunnerOptions
{
    public const string SectionName = "Evaluations:Sandbox";

    /// <summary>The Docker CLI, by name on the <c>PATH</c> or by path. Nothing checks it at startup.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DockerExecutable { get; set; } = "docker";
}
