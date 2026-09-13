namespace Ritocode.Shared.Contracts.Problems;

/// <summary>
/// Answers what is true about one problem version, for a module about to build on it.
/// </summary>
/// <remarks>
/// <para>
/// A cross-module contract in the sense of ADR 0007: declared here, implemented by the Problems
/// module, and taken as a constructor parameter by the module that asks — the Workspaces module,
/// which may not open the Problems <c>DbContext</c> and has no foreign key on
/// <c>workspaces.problem_version_id</c> to lean on (ADR 0004).
/// </para>
/// <para>
/// A draft is reported like any other version, with <see cref="ProblemVersionSummary.PublishedAt"/>
/// left <see langword="null"/>. Whether a workspace may be opened on a draft is the consumer's rule,
/// and it lives with the consumer's tests; putting it here would move it into the module nobody
/// looks in when the rule changes (ADR 0007 §2).
/// </para>
/// </remarks>
public interface IProblemVersionLookup
{
    /// <summary>
    /// The version with this identifier — exactly that version, not its problem's latest — or
    /// <see langword="null"/> when there is no such row.
    /// </summary>
    Task<ProblemVersionSummary?> FindAsync(Guid id, CancellationToken cancellationToken);
}
