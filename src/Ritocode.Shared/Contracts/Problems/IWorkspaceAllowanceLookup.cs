namespace Ritocode.Shared.Contracts.Problems;

/// <summary>
/// What a problem version allows inside a workspace built from it: which files may change, and how
/// large the tree may be. The question a workspace file write asks (#12, #36).
/// </summary>
/// <remarks>
/// <para>
/// Its own interface rather than more fields on <see cref="IProblemVersionLookup"/> (ADR 0007 §1).
/// Opening a workspace needs the bundle and whether the version is published; writing a file needs
/// neither, and needs this — a different subset, so a different question.
/// </para>
/// <para>
/// Facts, not policy (§2). The answer is the version's lists and numbers, draft or published alike;
/// what a write that breaks them is answered with is the Workspaces module's decision.
/// </para>
/// </remarks>
public interface IWorkspaceAllowanceLookup
{
    /// <summary>
    /// The allowance of version <paramref name="problemVersionId"/>, or <see langword="null"/> when
    /// no such version exists.
    /// </summary>
    Task<WorkspaceAllowance?> FindAsync(Guid problemVersionId, CancellationToken cancellationToken);
}
