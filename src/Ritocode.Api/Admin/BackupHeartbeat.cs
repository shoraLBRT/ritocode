using System.Globalization;

namespace Ritocode.Api.Admin;

/// <summary>
/// The heartbeat <c>deploy/backup.sh</c> leaves when a backup was dumped and copied off the server (#296,
/// ADR 0020): one line, the time in seconds since the epoch. <c>deploy/check.sh</c> alerts on it; the system
/// page shows it.
/// </summary>
internal static class BackupHeartbeat
{
    // 9999-12-31: anything later is not a time DateTimeOffset holds.
    private const long LastSecond = 253402300799;

    /// <summary>
    /// The last good backup, or <see langword="null"/> when there is no path, no file, or the file does not hold
    /// a time — none of them an error of the page.
    /// </summary>
    public static async Task<BackupInfo?> ReadAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds is > 0 and <= LastSecond
                ? new BackupInfo(DateTimeOffset.FromUnixTimeSeconds(seconds))
                : null;
    }
}
