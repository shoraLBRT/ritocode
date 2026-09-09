using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Packaging;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Problems.Ingest;

/// <summary>Ingest over the module's own schema and the shared object store.</summary>
/// <remarks>
/// <para>
/// Every ingest of a slug adds a version rather than replacing one. A published version is what a
/// workspace was created from, so overwriting one would change the task underneath an attempt
/// already in flight — the thing docs/DOMAIN_MODEL.md has versions in order to prevent.
/// </para>
/// <para>
/// The version is published as it is ingested. `published_at` stays nullable because a draft and
/// review flow is a real stage-two need, but nothing in the slice can move a version through one,
/// and a version nothing can publish is a version the catalog can never show.
/// </para>
/// </remarks>
public sealed class ProblemIngestService(
    ProblemsDbContext context,
    IObjectStore objectStore,
    TimeProvider timeProvider) : IProblemIngest
{
    public async Task<IngestedProblemVersion> IngestAsync(
        ProblemPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var manifest = package.Manifest;
        var slug = manifest.Slug.Trim().ToLowerInvariant();
        var now = timeProvider.GetUtcNow();

        var problem = await context.Problems
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

        if (problem is null)
        {
            problem = Problem.Create(slug, manifest.Title, manifest.Difficulty, package.Description, manifest.Tags, now);
            context.Problems.Add(problem);
        }
        else
        {
            // Everything a later revision may restate. The slug is not among them: it is the
            // problem's address, and it is what identified this row in the first place.
            problem.Title = manifest.Title;
            problem.Difficulty = manifest.Difficulty;
            problem.Description = package.Description;
            problem.Tags = manifest.Tags;
        }

        var previous = await context.ProblemVersions
            .Where(version => version.ProblemId == problem.Id)
            .MaxAsync(version => (int?)version.Version, cancellationToken)
            .ConfigureAwait(false);

        var problemVersion = ProblemVersion.Create(problem.Id, (previous ?? 0) + 1, package.ValidatorConfigJson, now);
        problemVersion.Publish(now);

        // The bundle is written before the row that points at it commits. The failure this ordering
        // leaves is an object no row references — write-once, keyed by an id nothing else can
        // produce, and therefore inert. The other ordering leaves a published version whose bundle
        // is missing, which every reader downstream would have to handle.
        await WriteBundleAsync(package, problemVersion.SnapshotReference, cancellationToken).ConfigureAwait(false);

        context.ProblemVersions.Add(problemVersion);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IngestedProblemVersion(
            problem.Id,
            problemVersion.Id,
            problem.Slug,
            problemVersion.Version,
            problemVersion.SnapshotReference);
    }

    private async Task WriteBundleAsync(
        ProblemPackage package,
        StorageReference reference,
        CancellationToken cancellationToken)
    {
        // A temporary file rather than memory: a put has to be signed over a known length, and a
        // package's own limits allow a workspace of up to 100 MiB (LimitsSpec.MaxMaxTotalBytes).
        // DeleteOnClose is what makes a failed ingest leave nothing behind on the host.
        var buffer = new FileStream(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            });

        await using (buffer.ConfigureAwait(false))
        {
            await ProblemBundleWriter.WriteAsync(package, buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;

            await objectStore.PutAsync(reference, buffer, cancellationToken).ConfigureAwait(false);
        }
    }
}
