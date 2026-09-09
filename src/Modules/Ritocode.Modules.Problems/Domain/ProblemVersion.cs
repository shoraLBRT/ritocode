using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Problems.Domain;

/// <summary>
/// One immutable revision of a problem. A workspace is created from a version, never from a
/// problem, so changing a problem never alters an in-flight attempt.
/// </summary>
public sealed class ProblemVersion
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    /// <summary>Monotonic per problem, starting at 1.</summary>
    public int Version { get; set; }

    /// <summary>
    /// Where the problem bundle lives, per docs/STORAGE_LAYOUT.md. Derived from <see cref="Id"/>
    /// once, at construction; every later read comes from this stored value rather than rebuilding
    /// the key, which is what lets the layout move without a data migration.
    /// </summary>
    public StorageReference SnapshotReference { get; set; } = null!;

    /// <summary>
    /// Validator pipeline configuration, stored as the canonical JSON of
    /// <see cref="Packaging.ValidatorPipeline"/> — see docs/PROBLEM_PACKAGE_SPEC.md. Opaque to the
    /// catalog; read by the evaluation pipeline.
    /// </summary>
    public string ValidatorConfig { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Null while the version is a draft. The catalog only ever resolves published versions.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public Problem? Problem { get; set; }

    /// <summary>
    /// Creates a draft version. The bundle key is built here rather than by the caller, so a
    /// version cannot exist whose <see cref="SnapshotReference"/> points somewhere other than at
    /// its own bundle.
    /// </summary>
    public static ProblemVersion Create(
        Guid problemId,
        int version,
        string validatorConfig,
        DateTimeOffset createdAt)
    {
        var id = Guid.CreateVersion7();

        return new ProblemVersion
        {
            Id = id,
            ProblemId = problemId,
            Version = version,
            SnapshotReference = StorageKeys.ProblemBundle(id),
            ValidatorConfig = validatorConfig,
            CreatedAt = createdAt.ToUniversalTime(),
            PublishedAt = null,
        };
    }

    /// <summary>
    /// Makes the version visible to the catalog. Publishing is idempotent: a version already
    /// published keeps the timestamp it was published with, because that timestamp orders the
    /// catalog and is not a detail of when someone last ran ingest.
    /// </summary>
    public void Publish(DateTimeOffset publishedAt) =>
        PublishedAt ??= publishedAt.ToUniversalTime();
}
