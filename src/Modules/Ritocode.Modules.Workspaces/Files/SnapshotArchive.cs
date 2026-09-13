using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Ritocode.Modules.Workspaces.Files;

/// <summary>
/// Reads and rewrites a workspace snapshot: the gzipped tar of docs/STORAGE_LAYOUT.md whose entry
/// names are workspace-relative paths.
/// </summary>
/// <remarks>
/// This module wrote every snapshot, and each is still read as untrusted — the rule
/// <c>StarterTree</c> applies to a bundle, applied on the way out. An entry that is not a regular
/// file, a path that could leave the tree, or a path held twice is a corrupt snapshot and throws,
/// rather than being skipped into a tree that looks fine and is not the one stored. A rewrite reads
/// through the same checks, so a corrupt snapshot is never saved back as a clean one.
/// </remarks>
internal static class SnapshotArchive
{
    /// <summary>Every file in <paramref name="snapshot"/>, ordered ordinally by path.</summary>
    /// <exception cref="InvalidDataException">The snapshot holds something a workspace may not.</exception>
    public static async Task<IReadOnlyList<SnapshotFile>> ListAsync(
        Stream snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var files = new List<SnapshotFile>();

        await foreach (var (path, entry) in FilesAsync(snapshot, copyData: false, cancellationToken))
        {
            files.Add(new SnapshotFile(path, entry.Length));
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

        await foreach (var (candidate, entry) in FilesAsync(snapshot, copyData: false, cancellationToken))
        {
            if (string.Equals(candidate, path, StringComparison.Ordinal))
            {
                return await ReadAllAsync(entry, cancellationToken);
            }
        }

        return null;
    }

    /// <summary>
    /// Copies <paramref name="snapshot"/> into <paramref name="destination"/> as a gzipped tar, with
    /// the bytes of the file at <paramref name="path"/> replaced by <paramref name="content"/>.
    /// </summary>
    /// <returns>
    /// The replaced file's previous bytes — <see langword="null"/> when the snapshot holds no such
    /// file, in which case nothing was replaced and the destination is a plain copy to discard — and
    /// the size of the tree as written.
    /// </returns>
    /// <remarks>Only ever replaces. A path the snapshot does not hold is not added.</remarks>
    /// <exception cref="InvalidDataException">The snapshot holds something a workspace may not.</exception>
    public static async Task<SnapshotReplacement> ReplaceAsync(
        Stream snapshot,
        string path,
        byte[] content,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(destination);

        byte[]? previous = null;
        var fileCount = 0;
        var totalBytes = 0L;

        await using (var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        await using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            // copyData, for StarterTree's reason: tar writes an entry's length ahead of its bytes and
            // neither gzip stream can seek, so each entry is buffered on its own before it is written.
            await foreach (var (name, entry) in FilesAsync(snapshot, copyData: true, cancellationToken))
            {
                fileCount++;

                if (string.Equals(name, path, StringComparison.Ordinal))
                {
                    previous = await ReadAllAsync(entry, cancellationToken);

                    using var replacement = new MemoryStream(content, writable: false);
                    await WriteEntryAsync(writer, name, replacement, modifiedAt: null, cancellationToken);
                    totalBytes += content.LongLength;
                }
                else
                {
                    await WriteEntryAsync(writer, name, entry.DataStream, entry.ModificationTime, cancellationToken);
                    totalBytes += entry.Length;
                }
            }
        }

        return new SnapshotReplacement(previous, fileCount, totalBytes);
    }

    private static async IAsyncEnumerable<(string Path, TarEntry Entry)> FilesAsync(
        Stream snapshot,
        bool copyData,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // leaveOpen: the stream belongs to the caller. Without copyData an entry's bytes stay
        // readable only until the next entry, which is enough for a list or a single read.
        await using var gzip = new GZipStream(snapshot, CompressionMode.Decompress, leaveOpen: true);
        await using var reader = new TarReader(gzip, leaveOpen: true);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.GetNextEntryAsync(copyData, cancellationToken) is { } entry)
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

    private static async Task<byte[]> ReadAllAsync(TarEntry entry, CancellationToken cancellationToken)
    {
        if (entry.DataStream is null)
        {
            return [];
        }

        using var buffer = new MemoryStream();
        await entry.DataStream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    private static Task WriteEntryAsync(
        TarWriter writer,
        string path,
        Stream? data,
        DateTimeOffset? modifiedAt,
        CancellationToken cancellationToken)
    {
        // Only the path and the bytes carry over, as in StarterTree: the mode is the writer's default,
        // so nothing in a workspace becomes executable by being saved.
        var copy = new PaxTarEntry(TarEntryType.RegularFile, path) { DataStream = data };

        if (modifiedAt is { } timestamp)
        {
            copy.ModificationTime = timestamp;
        }

        return writer.WriteEntryAsync(copy, cancellationToken);
    }
}

/// <param name="Path">Workspace-relative, forward-slashed.</param>
/// <param name="SizeBytes">The file's length in bytes.</param>
internal sealed record SnapshotFile(string Path, long SizeBytes);

/// <param name="Previous">The bytes replaced, or <see langword="null"/> when there was no such file.</param>
/// <param name="FileCount">Files in the tree as written.</param>
/// <param name="TotalBytes">Bytes in the tree as written.</param>
internal sealed record SnapshotReplacement(byte[]? Previous, int FileCount, long TotalBytes);
