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

        var cards = slugs.Count == 0
            ? []
            : await context.Cards.AsNoTracking()
                .Where(card => slugs.Contains(card.Slug))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var taxonomyRow = await context.Taxonomy.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var taxonomy = taxonomyRow is null ? Taxonomy.Empty : ContentJson.Read<Taxonomy>(taxonomyRow.Document);
        var labels = taxonomy.Texts.GetValueOrDefault(ContentRules.DefaultLocale);

        return new CardClasses(
            [.. taxonomy.Classes],
            cards.ToDictionary(card => card.Slug, card => card.Class, StringComparer.Ordinal),
            taxonomy.Classes.ToDictionary(id => id, id => labels?.Classes.GetValueOrDefault(id)?.Name ?? id, StringComparer.Ordinal),
            cards.ToDictionary(
                card => card.Slug,
                card => ContentJson.Read<Dictionary<string, CardText>>(card.Texts).GetValueOrDefault(ContentRules.DefaultLocale)?.Name ?? card.Slug,
                StringComparer.Ordinal));
    }
}
