using Ritocode.Modules.Workspaces.Files;

namespace Ritocode.Modules.Workspaces.Tests.Files;

/// <summary>What a file's revision is, and which strings can be one.</summary>
public sealed class FileRevisionTests
{
    private const string OrdersRevision = "498c35adbbf811ffe6bbb1cb0d84989baa8a7cea44e2426c6ddbd2997c9bdaf5";

    [Fact]
    public void Of_IsTheSha256OfTheBytes_AsLowerCaseHex()
    {
        // Computed independently with sha256sum, and the same value frontend/src/test/responses.ts
        // carries for this text — so the fixture there is a response the API could actually send.
        Assert.Equal(OrdersRevision, FileRevision.Of("namespace Orders;\r\n"u8));
    }

    [Fact]
    public void Of_TellsApartBytesThatAnEditorWouldShowAlike()
    {
        // A byte-order mark and a line ending are invisible in most editors and are still a change.
        var plain = FileRevision.Of("namespace Orders;\n"u8);

        Assert.NotEqual(plain, FileRevision.Of("namespace Orders;\r\n"u8));
        Assert.NotEqual(plain, FileRevision.Of([0xEF, 0xBB, 0xBF, .. "namespace Orders;\n"u8]));
    }

    [Theory]
    [InlineData(OrdersRevision, true)]
    [InlineData("498C35ADBBF811FFE6BBB1CB0D84989BAA8A7CEA44E2426C6DDBD2997C9BDAF5", false)]
    [InlineData("498c35adbbf811ffe6bbb1cb0d84989baa8a7cea44e2426c6ddbd2997c9bdaf", false)]
    [InlineData("498c35adbbf811ffe6bbb1cb0d84989baa8a7cea44e2426c6ddbd2997c9bdaf50", false)]
    [InlineData("g98c35adbbf811ffe6bbb1cb0d84989baa8a7cea44e2426c6ddbd2997c9bdaf5", false)]
    [InlineData("latest", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWellFormed_AcceptsExactlyTheShapeOfProduces(string? value, bool expected)
    {
        Assert.Equal(expected, FileRevision.IsWellFormed(value));
    }
}
