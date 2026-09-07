namespace Ritocode.Shared.Storage;

/// <summary>
/// The wire names of <see cref="StorageRole"/>. These appear in stored reference strings, so they
/// are fixed spellings rather than <c>Enum.ToString()</c> — renaming the C# member must not
/// silently invalidate every row already written.
/// </summary>
public static class StorageRoles
{
    private const string ProblemBundlesName = "problem-bundles";
    private const string WorkspaceSnapshotsName = "workspace-snapshots";
    private const string EvaluationArtifactsName = "evaluation-artifacts";

    /// <summary>Every role, in declaration order. Lets callers iterate without reflecting over the enum.</summary>
    public static IReadOnlyList<StorageRole> All { get; } =
        [StorageRole.ProblemBundles, StorageRole.WorkspaceSnapshots, StorageRole.EvaluationArtifacts];

    /// <summary>The name this role is written as inside a reference.</summary>
    public static string Name(this StorageRole role) => role switch
    {
        StorageRole.ProblemBundles => ProblemBundlesName,
        StorageRole.WorkspaceSnapshots => WorkspaceSnapshotsName,
        StorageRole.EvaluationArtifacts => EvaluationArtifactsName,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown storage role."),
    };

    /// <summary>
    /// Reads a role back from a stored reference. Returns <see langword="false"/> for anything
    /// unrecognised rather than guessing: a reference naming a role this build does not know is a
    /// row from a newer layout, and resolving it to the wrong bucket is worse than failing.
    /// </summary>
    public static bool TryParse(string? name, out StorageRole role)
    {
        switch (name)
        {
            case ProblemBundlesName:
                role = StorageRole.ProblemBundles;
                return true;
            case WorkspaceSnapshotsName:
                role = StorageRole.WorkspaceSnapshots;
                return true;
            case EvaluationArtifactsName:
                role = StorageRole.EvaluationArtifacts;
                return true;
            default:
                role = default;
                return false;
        }
    }
}
