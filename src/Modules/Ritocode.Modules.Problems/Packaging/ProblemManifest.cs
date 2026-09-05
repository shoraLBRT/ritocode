using System.Text.Json.Nodes;
using Ritocode.Modules.Problems.Domain;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// A <c>problem.yaml</c> exactly as authored, per docs/PROBLEM_PACKAGE_SPEC.md.
/// </summary>
/// <remarks>
/// This is the parsed shape, not a validated one: fields a manifest is required to state carry no
/// default here and are nullable so that "absent" and "written as zero" stay distinguishable.
/// <see cref="ProblemManifestValidator"/> decides whether an instance is usable, and
/// <see cref="ProblemPackageLoader"/> is what consumers should go through.
/// </remarks>
public sealed record ProblemManifest
{
    /// <summary>The only <c>schema_version</c> this codebase understands.</summary>
    public const int CurrentSchemaVersion = 1;

    public const int SlugMaxLength = 128;
    public const int TitleMaxLength = 200;
    public const int LanguageMaxLength = 32;
    public const int TagMaxLength = 32;
    public const int MaxTags = 8;
    public const int MaxHints = 5;
    public const int HintMaxLength = 280;
    public const int MaxValidators = 8;

    public int? SchemaVersion { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public Difficulty Difficulty { get; init; }

    /// <summary>Runner image selector. The registry of known languages belongs to #22.</summary>
    public string Language { get; init; } = string.Empty;

    public string[] Tags { get; init; } = [];

    /// <summary>Package-relative path to the Markdown description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Shown in the order written.</summary>
    public string[] Hints { get; init; } = [];

    public WorkspaceSpec Workspace { get; init; } = new();

    public LimitsSpec Limits { get; init; } = new();

    /// <summary>The validator pipeline, in execution order.</summary>
    public ValidatorSpec[] Validators { get; init; } = [];

    public FixturesSpec Fixtures { get; init; } = new();
}

/// <summary>
/// The tree the user opens, and which parts of it they may change. A file matched by neither list
/// or by both is a package error — see docs/PROBLEM_PACKAGE_SPEC.md.
/// </summary>
public sealed record WorkspaceSpec
{
    public const string DefaultRoot = "starter";

    /// <summary>Package-relative directory materialised into the workspace.</summary>
    public string Root { get; init; } = DefaultRoot;

    /// <summary>Globs, relative to <see cref="Root"/>, of files the user may change.</summary>
    public string[] Editable { get; init; } = [];

    /// <summary>
    /// Globs of files the user sees but may not change. The orchestrator restores them from the
    /// package before the validators run, so a tampered submission is still graded honestly.
    /// </summary>
    public string[] Readonly { get; init; } = [];
}

/// <summary>
/// Bounds on the workspace content. Execution limits — cpu, memory, pids, network — belong to the
/// sandbox and are deliberately not authorable here.
/// </summary>
public sealed record LimitsSpec
{
    public const int DefaultMaxFiles = 200;
    public const int MaxMaxFiles = 2_000;
    public const int DefaultMaxFileBytes = 256 * 1024;
    public const int MaxMaxFileBytes = 4 * 1024 * 1024;
    public const int DefaultMaxTotalBytes = 5 * 1024 * 1024;
    public const int MaxMaxTotalBytes = 100 * 1024 * 1024;

    public int MaxFiles { get; init; } = DefaultMaxFiles;

    public int MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    public int MaxTotalBytes { get; init; } = DefaultMaxTotalBytes;
}

/// <summary>One step of the validator pipeline, as authored.</summary>
public sealed record ValidatorSpec
{
    public const int IdMaxLength = 32;
    public const int TypeMaxLength = 32;
    public const int MaxTimeoutSeconds = 900;

    /// <summary>Stable identifier of this step in the report. Unique within the pipeline.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Selects the validator plugin. Unknown types are rejected by the plugin registry (#18).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Share of the score, 0–100. The weights of a pipeline sum to exactly 100.</summary>
    public int? Weight { get; init; }

    /// <summary>A failed required step fails the submission whatever the score.</summary>
    public bool Required { get; init; } = true;

    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Plugin configuration, carried but never interpreted here. Keys are sorted ordinally while
    /// parsing so that the stored pipeline is byte-identical however the YAML was written.
    /// </summary>
    public JsonObject? With { get; init; }
}

/// <summary>
/// Known-good and known-bad answers, overlaid on the workspace tree. Content only: fixtures are
/// never served to a user.
/// </summary>
public sealed record FixturesSpec
{
    /// <summary>Package-relative directory holding an answer that must pass.</summary>
    public string? Passing { get; init; }

    /// <summary>Package-relative directory holding an answer that must fail.</summary>
    public string? Failing { get; init; }
}
