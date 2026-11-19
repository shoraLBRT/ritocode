using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Content.Contracts;

/// <summary>
/// The Content module's answer to <see cref="IContentRevisionLookup"/>: every ingest rewrites the one taxonomy
/// row, so its revision and its time are the last ingest's.
/// </summary>
internal sealed class ContentRevisionLookup(ContentDbContext context) : IContentRevisionLookup
{
    public async Task<ContentRevision?> LastAsync(CancellationToken cancellationToken)
    {
        var row = await context.Taxonomy.AsNoTracking()
            .Where(taxonomy => taxonomy.Id == StoredTaxonomy.SingletonId)
            .Select(taxonomy => new { taxonomy.ContentRevision, taxonomy.UpdatedAt })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : new ContentRevision(row.ContentRevision, row.UpdatedAt);
    }
}
