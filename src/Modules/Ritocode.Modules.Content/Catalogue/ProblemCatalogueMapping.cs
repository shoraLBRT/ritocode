using Ritocode.Modules.Content.Format;

namespace Ritocode.Modules.Content.Catalogue;

/// <summary>
/// The problem catalogue as it is shown (docs/SPEC.md §4.2): every class in the taxonomy's order with
/// its name, and every card in full, by class and then slug, in the default locale. One mapping for
/// the two places the catalogue leaves the backend — <c>GET /problems</c>, from the database, and the
/// content export the frontend prerenders <c>/problems</c> from, straight from the files — so the
/// static page and the API cannot drift apart.
/// </summary>
public static class ProblemCatalogueMapping
{
    /// <param name="cards">The live cards. A card with no text in the default locale is left out.</param>
    public static ProblemCatalogueView Map(Taxonomy taxonomy, IEnumerable<ProblemCard> cards)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);
        ArgumentNullException.ThrowIfNull(cards);

        var labels = taxonomy.Texts.GetValueOrDefault(ContentRules.DefaultLocale);

        var classes = taxonomy.Classes
            .Select(id => labels?.Classes.GetValueOrDefault(id) is { } label
                ? new ClassView(id, label.Name, label.Description)
                : new ClassView(id, id, null))
            .ToList();

        var views = cards
            .OrderBy(card => ClassRank(taxonomy, card.Class))
            .ThenBy(card => card.Slug, StringComparer.Ordinal)
            .Select(card => (card, text: card.Texts.GetValueOrDefault(ContentRules.DefaultLocale)))
            .Where(pair => pair.text is not null)
            .Select(pair => ToView(pair.card, pair.text!))
            .ToList();

        return new ProblemCatalogueView(classes, views);
    }

    private static CardView ToView(ProblemCard card, CardText text) => new(
        card.Slug,
        card.Class,
        text.Name,
        text.Summary,
        text.Keywords,
        new CardSectionsView(
            text.Sections.GetValueOrDefault(CardSection.Signs),
            text.Sections.GetValueOrDefault(CardSection.WhyAiDoesIt),
            text.Sections.GetValueOrDefault(CardSection.Cost),
            text.Sections.GetValueOrDefault(CardSection.AcceptableWhen),
            text.Sections.GetValueOrDefault(CardSection.Detection),
            text.Sections.GetValueOrDefault(CardSection.Treatment),
            text.Sections.GetValueOrDefault(CardSection.Sources),
            text.Sections.GetValueOrDefault(CardSection.CounterArguments)));

    private static int ClassRank(Taxonomy taxonomy, string id)
    {
        var rank = taxonomy.Classes.ToList().IndexOf(id);
        return rank < 0 ? int.MaxValue : rank;
    }
}
