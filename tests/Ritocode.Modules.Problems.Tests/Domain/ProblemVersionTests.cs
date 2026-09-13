using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Tests.Domain;

/// <summary>What a version records about the workspace built from it. No database.</summary>
public sealed class ProblemVersionTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AVersionThatDeclaredNoWorkspace_HasNothingEditable()
    {
        // The safe reading of not knowing, and what a row from before the column existed reads as.
        var version = ProblemVersion.Create(Guid.CreateVersion7(), 1, "{}", "starter", Noon);

        Assert.Empty(version.EditableFiles);
    }

    [Fact]
    public void DeclareWorkspace_RecordsTheEditableFilesInOrdinalOrder_AndTheLimits()
    {
        var version = ProblemVersion.Create(Guid.CreateVersion7(), 1, "{}", "starter", Noon);

        version.DeclareWorkspace(
            ["src/b.cs", "src/B.cs", "src/a.cs"],
            new LimitsSpec { MaxFiles = 7, MaxFileBytes = 100, MaxTotalBytes = 500 });

        Assert.Equal(["src/B.cs", "src/a.cs", "src/b.cs"], version.EditableFiles);
        Assert.Equal(7, version.MaxFiles);
        Assert.Equal(100, version.MaxFileBytes);
        Assert.Equal(500, version.MaxTotalBytes);
    }

    [Fact]
    public void DeclareWorkspace_OnAPublishedVersion_Throws()
    {
        // A workspace may already be open on it; changing what it allows would move the rules under
        // an attempt in flight.
        var version = ProblemVersion.Create(Guid.CreateVersion7(), 1, "{}", "starter", Noon);
        version.Publish(Noon);

        Assert.Throws<InvalidOperationException>(() => version.DeclareWorkspace(["src/a.cs"], new LimitsSpec()));
    }
}
