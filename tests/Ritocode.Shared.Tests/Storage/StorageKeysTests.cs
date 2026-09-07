using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Tests.Storage;

/// <summary>
/// The layout in docs/STORAGE_LAYOUT.md, asserted literally. A key is the most expensive kind of
/// string to change, so these are spelled out rather than derived — a test that builds the expected
/// key the same way the code does would pass through any rename of the layout.
/// </summary>
public sealed class StorageKeysTests
{
    private static readonly Guid Id = Guid.Parse("018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60");

    [Fact]
    public void ProblemBundle_MatchesTheLayout()
    {
        Assert.Equal(
            "problem-bundles/problem-versions/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60/bundle.tar.gz",
            StorageKeys.ProblemBundle(Id).ToString());
    }

    [Fact]
    public void WorkspaceSnapshot_MatchesTheLayout()
    {
        Assert.Equal(
            "workspace-snapshots/workspaces/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60/tree.tar.gz",
            StorageKeys.WorkspaceSnapshot(Id).ToString());
    }

    [Fact]
    public void SubmissionArtifacts_IsAPrefix_BecauseTheFileSetGrowsWithTheValidators()
    {
        var reference = StorageKeys.SubmissionArtifacts(Id);

        Assert.Equal(
            "evaluation-artifacts/submissions/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60/",
            reference.ToString());
        Assert.True(reference.IsPrefix);
    }

    [Fact]
    public void SubmissionInputTree_IsUnderTheSubmissionPrefix_NotTheWorkspaceKey()
    {
        var reference = StorageKeys.SubmissionInputTree(Id);

        Assert.Equal(
            "evaluation-artifacts/submissions/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60/input/tree.tar.gz",
            reference.ToString());
        Assert.StartsWith(StorageKeys.SubmissionArtifacts(Id).ToString(), reference.ToString(), StringComparison.Ordinal);
        Assert.NotEqual(StorageKeys.WorkspaceSnapshot(Id).Role, reference.Role);
    }

    [Theory]
    [InlineData("stdout.txt")]
    [InlineData("stderr.txt")]
    [InlineData("output.tar.gz")]
    public void ValidatorArtifacts_MatchTheLayout(string fileName)
    {
        var reference = fileName switch
        {
            "stdout.txt" => StorageKeys.ValidatorStdout(Id, "unit-tests"),
            "stderr.txt" => StorageKeys.ValidatorStderr(Id, "unit-tests"),
            _ => StorageKeys.ValidatorOutput(Id, "unit-tests"),
        };

        Assert.Equal(
            "evaluation-artifacts/submissions/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60"
            + $"/validators/unit-tests/{fileName}",
            reference.ToString());
    }

    [Fact]
    public void EmptyIdentifier_IsRejected_BecauseEveryCallerWouldCollideOnOneKey()
    {
        Assert.Throws<ArgumentException>(() => StorageKeys.ProblemBundle(Guid.Empty));
        Assert.Throws<ArgumentException>(() => StorageKeys.WorkspaceSnapshot(Guid.Empty));
        Assert.Throws<ArgumentException>(() => StorageKeys.SubmissionArtifacts(Guid.Empty));
        Assert.Throws<ArgumentException>(() => StorageKeys.SubmissionInputTree(Guid.Empty));
    }

    [Theory]
    [InlineData("Unit-Tests")]                          // upper case
    [InlineData("unit_tests")]                          // underscore is not in the manifest slug
    [InlineData("../escape")]
    [InlineData("unit tests")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // 33 characters; the manifest limit is 32
    public void ValidatorId_IsRecheckedWhereTheKeyIsBuilt_NotOnlyWhereItWasParsed(string validatorId)
    {
        Assert.Throws<ArgumentException>(() => StorageKeys.ValidatorStdout(Id, validatorId));
        Assert.Throws<ArgumentException>(() => StorageKeys.ValidatorStderr(Id, validatorId));
        Assert.Throws<ArgumentException>(() => StorageKeys.ValidatorOutput(Id, validatorId));
    }

    [Fact]
    public void LongestKeyTheLayoutCanProduce_IsThe127CharactersTheDocumentClaims()
    {
        // docs/STORAGE_LAYOUT.md rule 4 states this number as the reason varchar(512) has margin.
        // If a new key class ever exceeds it, the document is wrong before the column is.
        var longest = StorageKeys.ValidatorOutput(Id, new string('a', 32));

        Assert.Equal(127, longest.ToString().Length);
        Assert.True(longest.ToString().Length <= StorageReference.MaxLength);
    }

    [Fact]
    public void Identifiers_AreLowerCaseCanonicalUuids()
    {
        var upperCase = Guid.Parse("018F3A2C-6B4E-7C31-9D05-2A1F4E8B7C60");

        Assert.Equal(StorageKeys.ProblemBundle(Id), StorageKeys.ProblemBundle(upperCase));
    }
}
