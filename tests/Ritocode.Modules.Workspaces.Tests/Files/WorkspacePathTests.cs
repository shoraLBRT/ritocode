using Ritocode.Modules.Workspaces.Files;

namespace Ritocode.Modules.Workspaces.Tests.Files;

/// <summary>The one path rule a bundle, a snapshot and a request are all held to.</summary>
public sealed class WorkspacePathTests
{
    [Theory]
    [InlineData("Program.cs")]
    [InlineData("src/App.cs")]
    [InlineData(".gitkeep")]
    [InlineData("src/.editorconfig")]
    [InlineData("..hidden")]
    [InlineData("a..b/c.cs")]
    [InlineData("src/Übertrag.cs")]
    public void APathInsideTheTree_IsConfined(string path)
    {
        // Dots are only dangerous as a whole segment; a name that merely contains them is a name.
        Assert.True(WorkspacePath.IsConfined(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/etc/passwd")]
    [InlineData("src/")]
    [InlineData("src//App.cs")]
    [InlineData("./App.cs")]
    [InlineData("src/./App.cs")]
    [InlineData("../problem.yaml")]
    [InlineData("src/../../problem.yaml")]
    [InlineData("src/..")]
    [InlineData("src\\App.cs")]
    [InlineData("C:\\Windows\\win.ini")]
    public void APathThatCouldLeaveTheTree_IsNotConfined(string? path)
    {
        Assert.False(WorkspacePath.IsConfined(path));
    }
}
