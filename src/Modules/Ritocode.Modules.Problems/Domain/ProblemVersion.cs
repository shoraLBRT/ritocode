namespace Ritocode.Modules.Problems.Domain;

/// <summary>
/// One immutable revision of a problem. A workspace is created from a version, never from a
/// problem, so changing a problem never alters an in-flight attempt.
/// </summary>
public sealed class ProblemVersion
{
    public const int SnapshotReferenceMaxLength = 512;

    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    /// <summary>Monotonic per problem, starting at 1.</summary>
    public int Version { get; set; }

    /// <summary>Object storage key of the problem bundle, per docs/ARCHITECTURE.md.</summary>
    public string SnapshotReference { get; set; } = string.Empty;

    /// <summary>
    /// Validator pipeline configuration, stored as JSON. Its schema is defined by
    /// <see href="https://github.com/shoraLBRT/ritocode/issues/8">#8</see>; until then this
    /// column is opaque to the catalog and read only by the evaluation pipeline.
    /// </summary>
    public string ValidatorConfig { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Null while the version is a draft. The catalog only ever resolves published versions.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public Problem? Problem { get; set; }

    public static ProblemVersion Create(
        Guid problemId,
        int version,
        string snapshotReference,
        string validatorConfig,
        DateTimeOffset createdAt) => new()
        {
            Id = Guid.CreateVersion7(),
            ProblemId = problemId,
            Version = version,
            SnapshotReference = snapshotReference,
            ValidatorConfig = validatorConfig,
            CreatedAt = createdAt.ToUniversalTime(),
            PublishedAt = null,
        };
}
