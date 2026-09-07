using System.ComponentModel.DataAnnotations;
using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Tests.Storage;

public sealed class ObjectStorageOptionsTests
{
    [Fact]
    public void EveryRole_ResolvesToABucket()
    {
        // Guards the switch in BucketFor against a role added to the enum and forgotten here,
        // which would be an unhandled exception on the first put rather than a compile error.
        var options = new ObjectStorageOptions();

        foreach (var role in StorageRoles.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(options.BucketFor(role)));
        }

        Assert.Equal(Enum.GetValues<StorageRole>().Length, StorageRoles.All.Count);
    }

    [Fact]
    public void Defaults_AreTheLocalBucketsComposeCreates()
    {
        var options = new ObjectStorageOptions();

        Assert.Equal("problem-bundles", options.BucketFor(StorageRole.ProblemBundles));
        Assert.Equal("workspace-snapshots", options.BucketFor(StorageRole.WorkspaceSnapshots));
        Assert.Equal("evaluation-artifacts", options.BucketFor(StorageRole.EvaluationArtifacts));
    }

    [Fact]
    public void Defaults_Validate_SoAHostStartsWithoutObjectStorageConfiguration()
    {
        Assert.Empty(Validate(new ObjectStorageOptions()));
    }

    [Fact]
    public void NoCredentials_MeansTheSdkDefaultChain_NotAnError()
    {
        var options = new ObjectStorageOptions();

        Assert.False(options.HasStaticCredentials);
        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData("key", "")]
    [InlineData("", "secret")]
    public void HalfACredentialPair_FailsAtStartup_NotAsASignatureError(string accessKey, string secretKey)
    {
        var results = Validate(new ObjectStorageOptions { AccessKey = accessKey, SecretKey = secretKey });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ObjectStorageOptions.AccessKey)));
    }

    [Fact]
    public void TwoRolesSharingABucket_IsRejected()
    {
        var results = Validate(new ObjectStorageOptions
        {
            ProblemBundlesBucket = "shared",
            WorkspaceSnapshotsBucket = "shared",
        });

        Assert.Contains(results, r => r.ErrorMessage!.Contains("bucket of its own", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UPPER")]
    [InlineData("ab")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("has space")]
    [InlineData("")]
    public void BucketNamesTheProviderWouldReject_FailAtStartup(string bucket)
    {
        var results = Validate(new ObjectStorageOptions { ProblemBundlesBucket = bucket });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ObjectStorageOptions.ProblemBundlesBucket)));
    }

    [Fact]
    public void PrefixedBucketNames_AreAccepted_BecauseRealS3NamesAreGloballyUnique()
    {
        var options = new ObjectStorageOptions
        {
            ProblemBundlesBucket = "ritocode-prod-problem-bundles",
            WorkspaceSnapshotsBucket = "ritocode-prod-workspace-snapshots",
            EvaluationArtifactsBucket = "ritocode-prod-evaluation-artifacts",
        };

        Assert.Empty(Validate(options));
        Assert.Equal("ritocode-prod-problem-bundles", options.BucketFor(StorageRole.ProblemBundles));
    }

    [Fact]
    public void ServiceUrlThatIsNotAUrl_FailsAtStartup()
    {
        var results = Validate(new ObjectStorageOptions { ServiceUrl = "localhost:59000" });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ObjectStorageOptions.ServiceUrl)));
    }

    private static List<ValidationResult> Validate(ObjectStorageOptions options)
    {
        var results = new List<ValidationResult>();

        // The same call ValidateDataAnnotations() makes, including IValidatableObject.
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        return results;
    }
}
