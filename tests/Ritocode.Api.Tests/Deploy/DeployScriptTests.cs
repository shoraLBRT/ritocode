using System.Diagnostics;

namespace Ritocode.Api.Tests.Deploy;

/// <summary>
/// The server's checks and their alert (#296) are shell scripts in <c>deploy/</c>, tested by
/// <c>deploy/check-test.sh</c> against fakes. Running it here makes every pull request run it, as the backend's
/// tests run on each one; the Monitor workflow runs it too, but only when <c>scripts/monitor-*.sh</c> change.
/// </summary>
public sealed class DeployScriptTests
{
    [Fact]
    public async Task TheServerChecks_AndTheirAlert_PassTheirTests()
    {
        var shell = FindShell();
        if (shell is null)
        {
            Assert.Skip("No POSIX sh here (on Windows, Git for Windows provides one).");
        }

        var script = Path.Combine(RepositoryRoot(), "deploy", "check-test.sh").Replace('\\', '/');
        var start = new ProcessStartInfo(shell, [script])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{shell} did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.True(process.ExitCode == 0, $"deploy/check-test.sh failed:\n{await output}\n{await errors}");
    }

    private static string? FindShell()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "sh";
        }

        // Git for Windows' sh, which puts its own tools (mktemp, awk, GNU date) on the PATH.
        string[] roots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ];
        return roots
            .Where(root => root.Length > 0)
            .Select(root => Path.Combine(root, "Git", "bin", "sh.exe"))
            .FirstOrDefault(File.Exists);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ritocode.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No Ritocode.slnx above {AppContext.BaseDirectory}.");
    }
}
