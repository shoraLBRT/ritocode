namespace Ritocode.Shared.Storage;

/// <summary>
/// The three roles object storage is divided into, per docs/STORAGE_LAYOUT.md. A role is not a
/// bucket name: the physical bucket is configuration (<see cref="ObjectStorageOptions"/>), because
/// bucket names are globally unique on real S3 and a deployment has to prefix them. The role is
/// what a stored reference carries, so an object copied between deployments keeps its key.
/// </summary>
public enum StorageRole
{
    /// <summary>One archive per published problem version. Written once by ingest.</summary>
    ProblemBundles,

    /// <summary>One archive per workspace: its current working tree. Overwritten on every save.</summary>
    WorkspaceSnapshots,

    /// <summary>Per submission: the frozen input tree and everything the evaluation produced.</summary>
    EvaluationArtifacts,
}
