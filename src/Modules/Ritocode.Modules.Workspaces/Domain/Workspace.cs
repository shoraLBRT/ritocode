namespace Ritocode.Modules.Workspaces.Domain;

/// <summary>
/// A user's working copy of a problem version. Owned by the Workspaces module.
/// </summary>
public sealed class Workspace
{
    public const int SnapshotReferenceMaxLength = 512;

    public Guid Id { get; set; }

    /// <summary>
    /// Owning user. Not a foreign key: <c>users</c> belongs to another module. The Workspaces
    /// module validates the user exists before creating a workspace.
    /// See docs/adr/0004-persistence-and-migrations.md.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The problem version this workspace was created from. Also not a foreign key, for the same
    /// reason — <c>problem_versions</c> belongs to the Problems module.
    /// </summary>
    public Guid ProblemVersionId { get; set; }

    /// <summary>Object storage key of the current working tree.</summary>
    public string SnapshotReference { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Last write to the working tree. Drives "continue where you left off" ordering.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    public static Workspace Create(
        Guid userId,
        Guid problemVersionId,
        string snapshotReference,
        DateTimeOffset createdAt)
    {
        var timestamp = createdAt.ToUniversalTime();

        return new Workspace
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ProblemVersionId = problemVersionId,
            SnapshotReference = snapshotReference,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };
    }
}
