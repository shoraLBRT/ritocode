using System.ComponentModel.DataAnnotations;

namespace Ritocode.Shared.Storage;

/// <summary>
/// Object storage settings, bound from the <c>ObjectStorage</c> configuration section and validated
/// at startup so a misconfigured deployment fails immediately rather than on the first put.
/// </summary>
/// <remarks>
/// This is the option shape docs/STORAGE_LAYOUT.md defers to the storage client: a
/// <see cref="StorageRole"/> resolves to a physical bucket name here and nowhere else, because
/// bucket names are globally unique on real S3 and a deployment has to prefix them
/// (<c>ritocode-prod-problem-bundles</c>). Nothing inside a key ever names the environment.
/// </remarks>
public sealed class ObjectStorageOptions : IValidatableObject
{
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// Endpoint of the S3-compatible service, for example the MinIO in <c>compose.yaml</c>. Left
    /// empty for real AWS S3, where the endpoint is derived from <see cref="Region"/>.
    /// </summary>
    /// <remarks>Validated in <see cref="Validate"/> rather than by <c>[Url]</c>, which rejects the
    /// empty string — and empty is the meaningful value here, not a missing one.</remarks>
    public string ServiceUrl { get; init; } = string.Empty;

    /// <summary>
    /// Signing region. Required even against MinIO, which ignores it but still verifies the
    /// signature that was computed with it.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Region { get; init; } = "us-east-1";

    /// <summary>
    /// Static access key. Left empty together with <see cref="SecretKey"/> to fall back to the
    /// SDK's default credential chain, which is how a deployment on AWS uses an instance role
    /// instead of keys in configuration.
    /// </summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>Static secret key. See <see cref="AccessKey"/>.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Physical bucket for <see cref="StorageRole.ProblemBundles"/>.</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression(BucketNamePattern, ErrorMessage = BucketNameMessage)]
    public string ProblemBundlesBucket { get; init; } = "problem-bundles";

    /// <summary>Physical bucket for <see cref="StorageRole.WorkspaceSnapshots"/>.</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression(BucketNamePattern, ErrorMessage = BucketNameMessage)]
    public string WorkspaceSnapshotsBucket { get; init; } = "workspace-snapshots";

    /// <summary>Physical bucket for <see cref="StorageRole.EvaluationArtifacts"/>.</summary>
    [Required(AllowEmptyStrings = false)]
    [RegularExpression(BucketNamePattern, ErrorMessage = BucketNameMessage)]
    public string EvaluationArtifactsBucket { get; init; } = "evaluation-artifacts";

    /// <summary>
    /// Whether keys address a bucket as a path segment rather than a host label. MinIO and most
    /// other S3-compatible services need this; virtual-host addressing would demand a DNS record
    /// per bucket.
    /// </summary>
    public bool UsePathStyleAddressing { get; init; } = true;

    // S3 bucket naming rules, narrowed to the subset that is legal everywhere: lower case,
    // 3 to 63 characters, starting and ending alphanumeric. A name rejected by the provider is
    // otherwise discovered on the first request rather than at startup.
    private const string BucketNamePattern = "^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$";
    private const string BucketNameMessage =
        "Bucket names must be 3-63 lower-case characters, starting and ending with a letter or digit.";

    /// <summary>The bucket a role resolves to.</summary>
    public string BucketFor(StorageRole role) => role switch
    {
        StorageRole.ProblemBundles => ProblemBundlesBucket,
        StorageRole.WorkspaceSnapshots => WorkspaceSnapshotsBucket,
        StorageRole.EvaluationArtifacts => EvaluationArtifactsBucket,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown storage role."),
    };

    /// <summary>True when static credentials were supplied rather than left to the SDK's chain.</summary>
    public bool HasStaticCredentials => AccessKey.Length > 0 && SecretKey.Length > 0;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ServiceUrl.Length > 0
            && !(Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var serviceUri)
                 && (serviceUri.Scheme == Uri.UriSchemeHttp || serviceUri.Scheme == Uri.UriSchemeHttps)))
        {
            yield return new ValidationResult(
                "ServiceUrl must be an absolute http or https URL, or empty to use the endpoint for Region.",
                [nameof(ServiceUrl)]);
        }

        // Half a credential pair is always a mistake, and the SDK would answer it with an opaque
        // signature failure on the first request instead of a startup error naming the setting.
        if (AccessKey.Length > 0 != SecretKey.Length > 0)
        {
            yield return new ValidationResult(
                "AccessKey and SecretKey must be set together, or both left empty to use the SDK's default credential chain.",
                [nameof(AccessKey), nameof(SecretKey)]);
        }

        // Two roles sharing a bucket would let a workspace snapshot and a problem bundle collide on
        // one key, and it defeats the reason there are three buckets: three different answers to
        // when an object may be deleted.
        var buckets = StorageRoles.All.Select(BucketFor).ToArray();
        if (buckets.Distinct(StringComparer.Ordinal).Count() != buckets.Length)
        {
            yield return new ValidationResult(
                "Each storage role needs a bucket of its own; two roles are configured to share one.",
                [nameof(ProblemBundlesBucket), nameof(WorkspaceSnapshotsBucket), nameof(EvaluationArtifactsBucket)]);
        }
    }
}
