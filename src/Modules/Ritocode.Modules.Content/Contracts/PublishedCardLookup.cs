using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Content.Contracts;

/// <summary>The Content module's answer to <see cref="IPublishedCardLookup"/>, over its own schema.</summary>
internal sealed class PublishedCardLookup(ContentDbContext context) : IPublishedCardLookup
{
    public async Task<IReadOnlySet<string>> PublishedAsync(IReadOnlyCollection<string> cardSlugs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardSlugs);

        if (cardSlugs.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var slugs = cardSlugs.ToList();

        var published = await context.Cards.AsNoTracking()
            .Where(card => card.RetiredAt == null && slugs.Contains(card.Slug))
            .Select(card => card.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return published.ToHashSet(StringComparer.Ordinal);
    }
}
