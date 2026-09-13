using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Workspaces.Domain;

/// <summary>
/// A user's working copy of a problem version. Owned by the Workspaces module.
/// </summary>
public sealed class Workspace
{
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

    /// <summary>
    /// Where the current working tree lives, per docs/STORAGE_LAYOUT.md. Derived from
    /// <see cref="Id"/> once, at construction; every later read and every save uses this stored
    /// value rather than rebuilding the key.
    /// </summary>
    public StorageReference SnapshotReference { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Last write to the working tree. Drives "continue where you left off" ordering.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Creates a workspace. The snapshot key is built here rather than by the caller, so a
    /// workspace cannot exist whose <see cref="SnapshotReference"/> points at another one's tree.
    /// </summary>
    public static Workspace Create(
        Guid userId,
        Guid problemVersionId,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A workspace needs an owner.", nameof(userId));
        }

        if (problemVersionId == Guid.Empty)
        {
            throw new ArgumentException("A workspace is created from a problem version.", nameof(problemVersionId));
        }

        var id = Guid.CreateVersion7();
        var timestamp = ToStoredPrecision(createdAt);

        return new Workspace
        {
            Id = id,
            UserId = userId,
            ProblemVersionId = problemVersionId,
            SnapshotReference = StorageKeys.WorkspaceSnapshot(id),
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };
    }

    /// <summary>
    /// UTC, truncated to the microsecond a <c>timestamptz</c> holds. Without this, the workspace a
    /// create returns carries .NET's 100-nanosecond ticks and the same workspace read back carries
    /// PostgreSQL's microseconds — one resource, two different <c>createdAt</c> values, depending on
    /// which response a client happened to keep.
    /// </summary>
    private static DateTimeOffset ToStoredPrecision(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();

        return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMicrosecond), TimeSpan.Zero);
    }
}
