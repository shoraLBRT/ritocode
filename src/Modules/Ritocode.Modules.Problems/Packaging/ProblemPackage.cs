namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// A problem package that has been read and checked against docs/PROBLEM_PACKAGE_SPEC.md.
/// Produced only by <see cref="ProblemPackageLoader"/>; holding one means the package is valid.
/// </summary>
public sealed record ProblemPackage
{
    /// <summary>Absolute path of the package directory.</summary>
    public required string PackageDirectory { get; init; }

    public required ProblemManifest Manifest { get; init; }

    /// <summary>Contents of the Markdown file named by <c>description</c>.</summary>
    public required string Description { get; init; }

    public required ValidatorPipeline Pipeline { get; init; }

    /// <summary>Canonical JSON for <c>problem_versions.validator_config</c>.</summary>
    public required string ValidatorConfigJson { get; init; }

    /// <summary>Workspace-relative paths the user may change, ordered ordinally.</summary>
    public required IReadOnlyList<string> EditableFiles { get; init; }

    /// <summary>
    /// Workspace-relative paths the user sees but may not change, ordered ordinally. The
    /// orchestrator restores these from the package before the validators run.
    /// </summary>
    public required IReadOnlyList<string> ReadonlyFiles { get; init; }

    /// <summary>Everything materialised into a workspace, ordered ordinally.</summary>
    public IReadOnlyList<string> WorkspaceFiles =>
        [.. EditableFiles.Concat(ReadonlyFiles).Order(StringComparer.Ordinal)];
}
