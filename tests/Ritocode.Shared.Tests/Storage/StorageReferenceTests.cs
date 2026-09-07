using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Tests.Storage;

public sealed class StorageReferenceTests
{
    private const string BundleKey = "problem-versions/018f3a2c-6b4e-7c31-9d05-2a1f4e8b7c60/bundle.tar.gz";

    [Fact]
    public void ToString_IsRoleAndKey_NotAUrl()
    {
        var reference = StorageReference.Create(StorageRole.ProblemBundles, BundleKey);

        Assert.Equal($"problem-bundles/{BundleKey}", reference.ToString());
    }

    [Fact]
    public void TryParse_RoundTripsWhatCreateProduced()
    {
        var created = StorageReference.Create(StorageRole.EvaluationArtifacts, "submissions/a/input/tree.tar.gz");

        Assert.True(StorageReference.TryParse(created.ToString(), out var parsed));
        Assert.Equal(created.Role, parsed.Role);
        Assert.Equal(created.Key, parsed.Key);
        Assert.Equal(created, parsed);
    }

    [Theory]
    [InlineData("problem-bundles", StorageRole.ProblemBundles)]
    [InlineData("workspace-snapshots", StorageRole.WorkspaceSnapshots)]
    [InlineData("evaluation-artifacts", StorageRole.EvaluationArtifacts)]
    public void TryParse_ReadsEveryRoleByItsWireName(string roleName, StorageRole expected)
    {
        Assert.True(StorageReference.TryParse($"{roleName}/some/key.txt", out var reference));
        Assert.Equal(expected, reference.Role);
        Assert.Equal("some/key.txt", reference.Key);
    }

    [Fact]
    public void WireNames_AreTheNamesTheLayoutDocumentFixes()
    {
        // These strings are in rows the moment anything is written; renaming the enum member must
        // not silently change them.
        Assert.Equal("problem-bundles", StorageRole.ProblemBundles.Name());
        Assert.Equal("workspace-snapshots", StorageRole.WorkspaceSnapshots.Name());
        Assert.Equal("evaluation-artifacts", StorageRole.EvaluationArtifacts.Name());
    }

    [Fact]
    public void TrailingSlash_IsTheWholeObjectVersusPrefixDistinction()
    {
        var prefix = StorageReference.Create(StorageRole.EvaluationArtifacts, "submissions/abc/");
        var single = StorageReference.Create(StorageRole.EvaluationArtifacts, "submissions/abc");

        Assert.True(prefix.IsPrefix);
        Assert.False(single.IsPrefix);
    }

    [Theory]
    [InlineData("problem-bundle/key.txt")]      // role misspelled
    [InlineData("problem-bundles")]             // role with no key
    [InlineData("problem-bundles/")]            // separator but nothing after it
    [InlineData("/problem-bundles/key.txt")]    // leading slash
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RefusesAnythingItCannotResolveExactly(string? value)
    {
        Assert.False(StorageReference.TryParse(value, out _));
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("a/../../b.txt")]
    [InlineData("a/./b.txt")]
    [InlineData("a//b.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("a\b.txt")]
    [InlineData("with space.txt")]
    [InlineData("percent%2fencoded.txt")]
    [InlineData("UPPERCASE.txt")]
    [InlineData("a/b?query")]
    public void Keys_ThatCouldLeaveTheirPrefixOrNeedEncoding_AreRejected(string key)
    {
        Assert.Throws<ArgumentException>(() => StorageReference.Create(StorageRole.ProblemBundles, key));
        Assert.False(StorageReference.TryParse($"problem-bundles/{key}", out _));
    }

    [Fact]
    public void ReferenceLongerThanTheColumn_IsRejectedRatherThanTruncated()
    {
        var key = new string('a', StorageReference.MaxLength);

        var exception = Assert.Throws<ArgumentException>(
            () => StorageReference.Create(StorageRole.ProblemBundles, key));

        Assert.Contains(StorageReference.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_RefusesAReferenceLongerThanTheColumn()
    {
        var value = "problem-bundles/" + new string('a', StorageReference.MaxLength);

        Assert.False(StorageReference.TryParse(value, out _));
    }

    [Fact]
    public void ReferenceOfExactlyTheColumnWidth_IsAccepted()
    {
        const string Role = "problem-bundles/";
        var key = new string('a', StorageReference.MaxLength - Role.Length);

        var reference = StorageReference.Create(StorageRole.ProblemBundles, key);

        Assert.Equal(StorageReference.MaxLength, reference.ToString().Length);
    }
}
