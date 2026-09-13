using System.Formats.Tar;
using System.IO.Compression;
using Ritocode.Modules.Workspaces.Files;

namespace Ritocode.Modules.Workspaces.Lifecycle;

/// <summary>
/// Turns a problem bundle into a workspace snapshot: the files under the bundle's workspace root,
/// re-rooted so the snapshot's paths are the paths a user sees.
/// </summary>
/// <remarks>
/// <para>
/// The bundle keeps the package's own layout — manifest, description and the workspace root side by
/// side (docs/STORAGE_LAYOUT.md) — so it cannot be copied as the snapshot: a user's tree would then
/// hold the manifest, and every path in it would carry a directory that means nothing to them.
/// Where the root is comes from the Problems contract rather than from parsing the manifest here,
/// because the manifest format belongs to that module.
/// </para>
/// <para>
/// The bundle was written by ingest from a validated package, and it is still read as untrusted.
/// Only regular files are copied, and a path that could leave the tree is a corrupt bundle rather
/// than something to normalise: this is the code that decides what lands in a workspace, and a link
/// or a <c>..</c> in a workspace is the path traversal ADR 0005 forbids, arriving by a door nobody
/// watches.
/// </para>
/// </remarks>
internal static class StarterTree
{
    /// <summary>
    /// Copies the starter tree out of <paramref name="bundle"/> into <paramref name="destination"/>
    /// as a gzipped tar.
    /// </summary>
    /// <returns>The snapshot's paths, in the order written — the bundle's order, which is ordinal.</returns>
    /// <exception cref="InvalidDataException">
    /// The bundle holds something other than regular files under the root, a path that could leave
    /// the tree, the same path twice, or nothing under the root at all.
    /// </exception>
    public static async Task<IReadOnlyList<string>> WriteAsync(
        Stream bundle,
        string workspaceRoot,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(destination);

        if (!WorkspacePath.IsConfined(workspaceRoot))
        {
            throw new ArgumentException(
                $"'{workspaceRoot}' is not a bundle-relative directory.",
                nameof(workspaceRoot));
        }

        var prefix = workspaceRoot + "/";
        var written = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // leaveOpen throughout: both streams belong to the caller, who still has to rewind the
        // destination and upload it.
        await using var gzipIn = new GZipStream(bundle, CompressionMode.Decompress, leaveOpen: true);
        await using var reader = new TarReader(gzipIn, leaveOpen: true);

        await using (var gzipOut = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        await using (var writer = new TarWriter(gzipOut, TarEntryFormat.Pax, leaveOpen: true))
        {
            // copyData: tar writes an entry's length ahead of its bytes, and neither gzip stream can
            // seek, so each entry is buffered on its own before it is written. One file at a time, and
            // a package's limits bound how large a file may be.
            while (await reader.GetNextEntryAsync(copyData: true, cancellationToken) is { } entry)
            {
                if (!entry.Name.StartsWith(prefix, StringComparison.Ordinal)
                    || entry.EntryType == TarEntryType.Directory)
                {
                    continue;
                }

                var path = entry.Name[prefix.Length..];

                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                {
                    throw new InvalidDataException(
                        $"Bundle entry '{entry.Name}' is a {entry.EntryType}; a workspace holds regular files only.");
                }

                if (!WorkspacePath.IsConfined(path))
                {
                    throw new InvalidDataException(
                        $"Bundle entry '{entry.Name}' does not stay inside the workspace root.");
                }

                if (!seen.Add(path))
                {
                    throw new InvalidDataException($"Bundle holds '{entry.Name}' more than once.");
                }

                // Only the path and the bytes carry over. The mode is the writer's default rather
                // than the bundle's, so nothing in a workspace is marked executable by the content.
                var copy = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = entry.DataStream,
                    ModificationTime = entry.ModificationTime,
                };

                await writer.WriteEntryAsync(copy, cancellationToken);
                written.Add(path);
            }
        }

        if (written.Count == 0)
        {
            throw new InvalidDataException($"Bundle holds no files under '{workspaceRoot}'.");
        }

        return written;
    }
}
