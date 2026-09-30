using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Persistence;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Content.Contracts;

/// <summary>The Content module's answer to <see cref="ICardClassLookup"/>, over its own schema.</summary>
internal sealed class CardClassLookup(ContentDbContext context) : ICardClassLookup
{
    public async Task<CardClasses> FindAsync(IReadOnlyCollection<string> cardSlugs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cardSlugs);

        var slugs = cardSlugs.ToList();

        var classOf = slugs.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await context.Cards.AsNoTracking()
                .Where(card => slugs.Contains(card.Slug))
                .ToDictionaryAsync(card => card.Slug, card => card.Class, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);

        var taxonomy = await context.Taxonomy.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var classes = taxonomy is null ? [] : ContentJson.Read<Taxonomy>(taxonomy.Document).Classes.ToList();

        return new CardClasses(classes, classOf);
    }
}
