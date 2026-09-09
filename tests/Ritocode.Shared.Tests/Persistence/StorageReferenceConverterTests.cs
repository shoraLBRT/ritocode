using Ritocode.Shared.Persistence;
using Ritocode.Shared.Storage;

namespace Ritocode.Shared.Tests.Persistence;

/// <summary>
/// The conversion itself, without a database in the way. What it does inside a mapped column is
/// asserted where a column exists, in the Problems module's ingest tests.
/// </summary>
public sealed class StorageReferenceConverterTests
{
    private static readonly StorageReferenceConverter Converter = new();

    [Fact]
    public void AReference_IsStoredAsItsTextForm()
    {
        var reference = StorageKeys.ProblemBundle(Guid.CreateVersion7());

        var stored = Converter.ConvertToProvider(reference);

        Assert.Equal(reference.ToString(), stored);
    }

    [Fact]
    public void AStoredReference_ComesBackEqualToWhatWentIn()
    {
        var reference = StorageKeys.SubmissionArtifacts(Guid.CreateVersion7());

        var restored = Converter.ConvertFromProvider(reference.ToString());

        Assert.Equal(reference, restored);

        // A prefix reference and an object reference are told apart by the trailing slash alone,
        // so the round trip has to preserve it or the two forms collapse into one.
        Assert.True(((StorageReference)restored!).IsPrefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bundle.tar.gz")]
    [InlineData("no-such-role/problem-versions/x/bundle.tar.gz")]
    [InlineData("problem-bundles/../escaped")]
    public void AValueThisBuildCannotResolve_Throws(string stored)
    {
        // Deliberately not "null, quietly": a row whose reference cannot be read is a fault to
        // report, and the alternative is a module acting on a half-understood key.
        Assert.Throws<InvalidOperationException>(() => Converter.ConvertFromProvider(stored));
    }
}
