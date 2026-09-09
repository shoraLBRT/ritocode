using System.Formats.Tar;
using System.IO.Compression;
using Ritocode.Modules.Problems.Packaging;

namespace Ritocode.Modules.Problems.Ingest;

/// <summary>
/// Writes the problem bundle: the one archive a published version is materialised from, stored at
/// the key docs/STORAGE_LAYOUT.md gives it.
/// </summary>
/// <remarks>
/// <para>
/// The bundle is the manifest, the description and the workspace root — nothing else. Fixtures are
/// deliberately excluded: they are the known-good and known-bad answers, and an object that must
/// never be served to a user is safest as an object that does not exist. That exclusion is what
/// lets the bundle be served without filtering.
/// </para>
/// <para>
/// Entries keep their package-relative paths, so the bundle is a filtered copy of the package
/// rather than a second layout to keep in step with it. A reader finds the workspace tree the same
/// way the loader did: by reading <c>workspace.root</c> out of the manifest.
/// </para>
/// <para>
/// Entries are written in ordinal order, which makes the archive's <em>contents</em> stable. The
/// bytes are not, and are not required to be — tar records timestamps and gzip records its own.
/// Determinism is a property of the normalised projection of an evaluation
/// (ADR 0006 §6), never of an artifact's bytes.
/// </para>
/// </remarks>
internal static class ProblemBundleWriter
{
    public static async Task WriteAsync(
        ProblemPackage package,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(destination);

        // leaveOpen: the caller owns the destination and still has to rewind and upload it.
        var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        await using (gzip.ConfigureAwait(false))
        {
            var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true);
            await using (writer.ConfigureAwait(false))
            {
                foreach (var entryName in EntryNames(package))
                {
                    var source = Path.Combine(
                        package.PackageDirectory,
                        entryName.Replace('/', Path.DirectorySeparatorChar));

                    await writer.WriteEntryAsync(source, entryName, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Every path the bundle carries, package-relative and ordinally sorted. A description that
    /// lives inside the workspace root would otherwise be written twice, so the set is distinct.
    /// </summary>
    internal static IReadOnlyList<string> EntryNames(ProblemPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var workspaceRoot = package.Manifest.Workspace.Root.TrimEnd('/');

        return
        [
            .. new[] { ProblemPackageLoader.ManifestFileName, package.Manifest.Description }
                .Concat(package.WorkspaceFiles.Select(file => $"{workspaceRoot}/{file}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
        ];
    }
}
