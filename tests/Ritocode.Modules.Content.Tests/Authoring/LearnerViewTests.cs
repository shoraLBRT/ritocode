using Ritocode.Modules.Content.Authoring;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Ingest;
using Ritocode.Modules.Content.Tests.Format;

namespace Ritocode.Modules.Content.Tests.Authoring;

/// <summary>
/// The learner's view is what the blind smoke test answers from (docs/SPEC.md §7.1): everything the
/// task screen shows, and nothing it does not — above all, not the answer key.
/// </summary>
public sealed class LearnerViewTests
{
    private const string Task = "invoice-mailer-monthly";

    [Fact]
    public void TheView_CarriesTheTaskTheMaterialTheCardsAndTheWholeTree()
    {
        var content = Reference();
        var view = LearnerView.Render(content, Task)!;

        var text = content.Tasks.Single().Texts[ContentRules.DefaultLocale];
        Assert.StartsWith("# Счёт клиенту по почте\n", view, StringComparison.Ordinal);
        Assert.Contains("Difficulty: easy", view, StringComparison.Ordinal);
        Assert.Contains(text.Context.Trim(), view, StringComparison.Ordinal);
        Assert.Contains(text.Brief.Trim(), view, StringComparison.Ordinal);

        var file = content.Materials.Single().Files.Single();
        Assert.Contains("1 file(s), 20 lines. Declared dependencies: none.", view, StringComparison.Ordinal);
        Assert.Contains("```python\n" + file.Content, view, StringComparison.Ordinal);

        foreach (var card in content.Cards)
        {
            var cardText = card.Texts[ContentRules.DefaultLocale];
            Assert.Contains($"- `{card.Slug}` — **{cardText.Name}**. {cardText.Summary}\n", view, StringComparison.Ordinal);
        }

        var labels = content.Taxonomy.Texts[ContentRules.DefaultLocale];
        Assert.Contains("### Гигиена и безопасность", view, StringComparison.Ordinal);
        Assert.All(content.Taxonomy.LeafIds, leaf => Assert.Contains($"- `{leaf}` — ", view, StringComparison.Ordinal));
        Assert.Contains($"### `accept` — {labels.Branches["accept"].Name}", view, StringComparison.Ordinal);
    }

    [Fact]
    public void TheView_NeverCarriesTheKeyTheReviewOrTheFullCards()
    {
        var content = Reference();
        var view = LearnerView.Render(content, Task)!;

        var text = content.Tasks.Single().Texts[ContentRules.DefaultLocale];
        Assert.DoesNotContain(text.Notes["money-in-float"].Trim(), view, StringComparison.Ordinal);
        Assert.DoesNotContain(text.Lesson!.Trim(), view, StringComparison.Ordinal);
        Assert.DoesNotContain("findings", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", view, StringComparison.OrdinalIgnoreCase);

        // The only place a leaf of the key may appear is its own line of the tree.
        var keyLeaves = content.Tasks.Single().Findings.SelectMany(finding => finding.Leaves);
        Assert.All(keyLeaves, leaf => Assert.Single(Occurrences(view, $"`{leaf}`")));

        // A keyword may also be a word of a summary ("ключ" in "ключи"); only the others can leak.
        var summaries = string.Join('\n', content.Cards.Select(card => card.Texts[ContentRules.DefaultLocale].Summary));

        foreach (var card in content.Cards)
        {
            var cardText = card.Texts[ContentRules.DefaultLocale];
            Assert.All(cardText.Sections.Values, section => Assert.DoesNotContain(section.Trim(), view, StringComparison.Ordinal));
            Assert.All(
                cardText.Keywords.Where(keyword => !summaries.Contains(keyword, StringComparison.OrdinalIgnoreCase)),
                keyword => Assert.DoesNotContain(keyword, view, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AnEasyTask_OffersItsShortlist_AndAnyOtherTheWholeCatalogue()
    {
        var reference = Reference();
        var content = reference with { Cards = [.. reference.Cards, .. Filler(reference.Cards[0], 30)] };
        var easy = content.Tasks.Single();
        var medium = easy with { Difficulty = TaskDifficulty.Medium };

        var shortlist = Shortlist.For(easy.Slug, easy.Findings.Select(finding => finding.Card), content.Cards.Select(card => card.Slug));

        Assert.Equal(shortlist.Order(StringComparer.Ordinal), LearnerView.Candidates(content, easy).Select(card => card.Slug).Order(StringComparer.Ordinal));
        Assert.Equal(34, LearnerView.Candidates(content, medium).Count);

        var view = LearnerView.Render(content, Task)!;
        Assert.Contains($"The shortlist of this easy task: {shortlist.Count} cards.", view, StringComparison.Ordinal);
        Assert.Equal(shortlist.Count, Occurrences(view, "\n- `").Count - content.Taxonomy.LeafIds.Count() - 1);
    }

    [Fact]
    public void TheCards_ComeInTheCatalogueOrder_ClassThenSlug()
    {
        var content = Reference();

        Assert.Equal(
            ["hardcoded-config", "secrets-in-repo", "swallowed-error", "money-in-float"],
            LearnerView.Candidates(content, content.Tasks.Single()).Select(card => card.Slug));
    }

    [Fact]
    public void AFenceOutlastsTheBackticksInAFile()
    {
        var reference = Reference();
        var material = reference.Materials.Single();
        var tricky = material with { Files = [new MaterialFile("notes.md", "```\nnot the end\n````\n")] };
        var content = reference with { Materials = [tricky] };

        var view = LearnerView.Render(content, Task)!;

        Assert.Contains("`````markdown\n```\nnot the end\n````\n`````\n", view, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownTask_HasNoView()
    {
        Assert.Null(LearnerView.Render(Reference(), "no-such-task"));
    }

    private static ContentSet Reference()
    {
        var (content, report) = ContentLoader.Load(TempContent.ReferenceRoot);
        Assert.Empty(report.Errors);
        return content;
    }

    private static IEnumerable<ProblemCard> Filler(ProblemCard template, int count) =>
        Enumerable.Range(1, count).Select(number => template with { Slug = $"filler-{number:00}" });

    private static List<int> Occurrences(string text, string value)
    {
        var found = new List<int>();

        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            found.Add(index);
        }

        return found;
    }
}
