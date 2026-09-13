namespace Ritocode.Shared.Contracts.Workspaces;

/// <summary>
/// A workspace, when it belongs to a given user. The question a submission asks before it freezes a
/// workspace's tree (#14).
/// </summary>
/// <remarks>
/// <para>
/// The owner is part of the question rather than a field of the answer for the consumer to compare.
/// ADR 0005 forbids serving a workspace without checking it belongs to the caller, and the Workspaces
/// module keeps that check inside the query that finds the row. A contract answering any workspace by
/// id would carry the check out of the module that owns the row, into every consumer that remembers
/// it — which is the habit #35 replaced with a rule.
/// </para>
/// <para>
/// Still a fact, not policy (ADR 0007 §2): whether a workspace with that id is this user's. What the
/// consumer answers when it is not is the consumer's decision.
/// </para>
/// </remarks>
public interface IOwnedWorkspaceLookup
{
    /// <summary>
    /// The workspace <paramref name="workspaceId"/> when it belongs to <paramref name="userId"/>, and
    /// <see langword="null"/> both when it belongs to someone else and when it does not exist.
    /// </summary>
    Task<WorkspaceSummary?> FindAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken);
}
