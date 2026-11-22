using Ritocode.Api.Admin;

namespace Ritocode.Api.Tests.Endpoints;

/// <summary>The backup's heartbeat as the system page reads it (#296): what <c>deploy/backup.sh</c> writes.</summary>
public sealed class BackupHeartbeatTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("ritocode-heartbeat-").FullName;

    private string Path => System.IO.Path.Combine(directory, "last-good");

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task TheSecondsSinceTheEpoch_AreTheLastGoodBackup()
    {
        // As `date -u +%s` writes it, with its line break.
        await File.WriteAllTextAsync(Path, "1763780400\n", TestContext.Current.CancellationToken);

        var backup = await BackupHeartbeat.ReadAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(new BackupInfo(new DateTimeOffset(2025, 11, 22, 3, 0, 0, TimeSpan.Zero)), backup);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("-5")]
    [InlineData("99999999999999999999")]
    public async Task AFileThatHoldsNoTime_IsNoBackup(string text)
    {
        await File.WriteAllTextAsync(Path, text, TestContext.Current.CancellationToken);

        Assert.Null(await BackupHeartbeat.ReadAsync(Path, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    public async Task NoPath_OrNoFile_IsNoBackup(string? path)
    {
        var resolved = path == "missing" ? Path : path;

        Assert.Null(await BackupHeartbeat.ReadAsync(resolved, TestContext.Current.CancellationToken));
    }
}
