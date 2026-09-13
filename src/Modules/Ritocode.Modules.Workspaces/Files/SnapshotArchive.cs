using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>
/// Reads a workspace snapshot: the gzipped tar of docs/STORAGE_LAYOUT.md whose entry names are
/// workspace-relative paths.
/// </summary>
/// <remarks>
/// This module wrote every snapshot, and each is still read as untrusted — the rule
/// <c>StarterTree</c> applies to a bundle, applied on the way out. An entry that is not a regular
/// file, a path that could leave the tree, or a path held twice is a corrupt snapshot and throws,
/// rather than being skipped into a tree that looks fine and is not the one stored.
/// </remarks>
internal static class SnapshotArchive
{
    /// <summary>Every file in <paramref name="snapshot"/>, ordered ordinally by path.</summary>
    /// <exception cref="InvalidDataException">The snapshot holds something a workspace may not.</exception>
    public static async Task<IReadOnlyList<WorkspaceFileEntry>> ListAsync(
        Stream snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var files = new List<WorkspaceFileEntry>();

        await foreach (var (path, entry) in FilesAsync(snapshot, cancellationToken))
        {
            files.Add(new WorkspaceFileEntry(path, entry.Length));
        }

        // Sorted here rather than trusted from the archive, so the order a client sees does not
        // depend on which writer last saved the tree.
        files.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));

        return files;
    }

    /// <summary>The bytes of <paramref name="path"/>, or <see langword="null"/> when the snapshot holds no such file.</summary>
    /// <exception cref="InvalidDataException">The snapshot holds something a workspace may not.</exception>
    public static async Task<byte[]?> ReadAsync(
        Stream snapshot,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(path);

        await foreach (var (candidate, entry) in FilesAsync(snapshot, cancellationToken))
        {
            if (!string.Equals(candidate, path, StringComparison.Ordinal))
            {
                continue;
            }

            if (entry.DataStream is null)
            {
                return [];
            }

            using var buffer = new MemoryStream();
            await entry.DataStream.CopyToAsync(buffer, cancellationToken);

            return buffer.ToArray();
        }

        return null;
    }

    private static async IAsyncEnumerable<(string Path, TarEntry Entry)> FilesAsync(
        Stream snapshot,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // leaveOpen: the stream belongs to the caller. copyData: false, because an entry's bytes are
        // only wanted for the one file being read, and they stay readable until the next entry.
        await using var gzip = new GZipStream(snapshot, CompressionMode.Decompress, leaveOpen: true);
        await using var reader = new TarReader(gzip, leaveOpen: true);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
        {
            if (entry.EntryType == TarEntryType.Directory)
            {
                continue;
            }

            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                throw new InvalidDataException(
                    $"Snapshot entry '{entry.Name}' is a {entry.EntryType}; a workspace holds regular files only.");
            }

            if (!WorkspacePath.IsConfined(entry.Name))
            {
                throw new InvalidDataException($"Snapshot entry '{entry.Name}' is not a workspace path.");
            }

            if (!seen.Add(entry.Name))
            {
                throw new InvalidDataException($"Snapshot holds '{entry.Name}' more than once.");
            }

            yield return (entry.Name, entry);
        }
    }
}
