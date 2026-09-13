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

    /// <summary>
    /// The manifest's <c>workspace.root</c>, with no trailing slash: the directory inside the bundle
    /// whose files are the starter tree. Stored rather than re-read from the bundle's manifest
    /// because a workspace is materialised outside this module, and the manifest format is this
    /// module's to parse — see docs/STORAGE_LAYOUT.md.
    /// </summary>
    public string WorkspaceRoot { get; set; } = Packaging.WorkspaceSpec.DefaultRoot;

    /// <summary>
    /// Workspace-relative paths a user may change, ordered ordinally: the manifest's
    /// <c>workspace.editable</c> globs resolved against the starter tree at ingest. Stored resolved
    /// rather than as globs so that glob matching — format knowledge — stays in this module, and the
    /// module enforcing it on a write only compares paths.
    /// </summary>
    /// <remarks>
    /// Empty until <see cref="DeclareWorkspace"/> says otherwise. A version that never declared its
    /// workspace has nothing editable, which is also what a row written before this column existed
    /// reads as: refusing every write is the safe answer to not knowing.
    /// </remarks>
    public string[] EditableFiles { get; set; } = [];

    /// <summary>The manifest's <c>limits.max_files</c>.</summary>
    public int MaxFiles { get; set; } = Packaging.LimitsSpec.DefaultMaxFiles;

    /// <summary>The manifest's <c>limits.max_file_bytes</c>.</summary>
    public int MaxFileBytes { get; set; } = Packaging.LimitsSpec.DefaultMaxFileBytes;

    /// <summary>The manifest's <c>limits.max_total_bytes</c>.</summary>
    public int MaxTotalBytes { get; set; } = Packaging.LimitsSpec.DefaultMaxTotalBytes;

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
        string workspaceRoot,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var id = Guid.CreateVersion7();

        return new ProblemVersion
        {
            Id = id,
            ProblemId = problemId,
            Version = version,
            SnapshotReference = StorageKeys.ProblemBundle(id),
            ValidatorConfig = validatorConfig,
            WorkspaceRoot = workspaceRoot.TrimEnd('/'),
            CreatedAt = createdAt.ToUniversalTime(),
            PublishedAt = null,
        };
    }

    /// <summary>
    /// Records what a workspace built from this version may change and how large it may grow — the
    /// validated package's resolved editable files and its limits.
    /// </summary>
    /// <exception cref="InvalidOperationException">The version is already published.</exception>
    public void DeclareWorkspace(IEnumerable<string> editableFiles, Packaging.LimitsSpec limits)
    {
        ArgumentNullException.ThrowIfNull(editableFiles);
        ArgumentNullException.ThrowIfNull(limits);

        // A published version is what workspaces were opened on. Changing what it allows would change
        // the rules under an attempt already in flight — the thing versions exist to prevent.
        if (PublishedAt is not null)
        {
            throw new InvalidOperationException($"Problem version {Id} is published; its workspace rules are fixed.");
        }

        EditableFiles = [.. editableFiles.Order(StringComparer.Ordinal)];
        MaxFiles = limits.MaxFiles;
        MaxFileBytes = limits.MaxFileBytes;
        MaxTotalBytes = limits.MaxTotalBytes;
    }

    /// <summary>
    /// Makes the version visible to the catalog. Publishing is idempotent: a version already
    /// published keeps the timestamp it was published with, because that timestamp orders the
    /// catalog and is not a detail of when someone last ran ingest.
    /// </summary>
    public void Publish(DateTimeOffset publishedAt) =>
        PublishedAt ??= publishedAt.ToUniversalTime();
}
