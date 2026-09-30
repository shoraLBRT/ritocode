using System.Text;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Ingest;

namespace Ritocode.Modules.Content.Authoring;

/// <summary>
/// A task as the learner meets it, rendered from content files as Markdown: title, difficulty,
/// context, brief, the material with its overview, the cards to pick from — name and summary only,
/// the shortlist for an easy task — and the whole treatment tree (docs/SPEC.md §4.4).
/// <para>
/// It is the input of the blind smoke test (SPEC §7.1), so it carries exactly what the task screen
/// receives and nothing more: no answer key, no notes, no lesson, no card weight, no card section,
/// no keyword. The text is in the default locale.
/// </para>
/// </summary>
public static class LearnerView
{
    /// <summary>The task rendered for a learner, or <c>null</c> when no task has that slug.</summary>
    public static string? Render(ContentSet content, string taskSlug)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(taskSlug);

        var task = content.Tasks.SingleOrDefault(candidate => candidate.Slug == taskSlug);
        var text = task?.Texts.GetValueOrDefault(ContentRules.DefaultLocale);
        var material = task is null ? null : content.Materials.SingleOrDefault(candidate => candidate.Slug == task.Material);

        if (task is null || text is null || material is null)
        {
            return null;
        }

        var labels = content.Taxonomy.Texts.GetValueOrDefault(ContentRules.DefaultLocale);
        var output = new StringBuilder();

        output.Append("# ").Line(text.Title).Line();
        output.Append("Difficulty: ").Line(Difficulty(task.Difficulty)).Line();
        output.Line("## Context").Line().Line(text.Context.Trim()).Line();
        output.Line("## Brief").Line().Line(text.Brief.Trim()).Line();

        AppendMaterial(output, material);
        AppendCandidates(output, content, task, labels);
        AppendTreatments(output, content.Taxonomy, labels);

        return output.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// The cards step 1 offers, in the catalogue's order — class, then slug: the shortlist for an
    /// easy task, the whole catalogue for any other.
    /// </summary>
    public static IReadOnlyList<ProblemCard> Candidates(ContentSet content, DiagnosisTask task)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(task);

        IEnumerable<ProblemCard> cards = content.Cards;

        if (task.Difficulty == TaskDifficulty.Easy)
        {
            var shortlist = Shortlist.For(
                task.Slug,
                task.Findings.Select(finding => finding.Card),
                content.Cards.Select(card => card.Slug)).ToHashSet(StringComparer.Ordinal);

            cards = cards.Where(card => shortlist.Contains(card.Slug));
        }

        var classes = content.Taxonomy.Classes.ToList();

        return [.. cards
            .OrderBy(card => classes.IndexOf(card.Class) is var rank and >= 0 ? rank : int.MaxValue)
            .ThenBy(card => card.Slug, StringComparer.Ordinal)];
    }

    private static void AppendMaterial(StringBuilder output, Material material)
    {
        var overview = MaterialOverview.Of(material);
        var dependencies = overview.Dependencies.Count == 0 ? "none" : string.Join(", ", overview.Dependencies);

        output.Line("## Material").Line();
        output.Line(FormattableString.Invariant(
            $"{overview.FileCount} file(s), {overview.TotalLines} lines. Declared dependencies: {dependencies}."));
        output.Line();

        foreach (var file in overview.Files)
        {
            output.Line(FormattableString.Invariant($"- `{file.Path}` — {file.Lines} lines"));
        }

        output.Line();

        foreach (var file in material.Files)
        {
            var fence = Fence(file.Content);

            output.Append("### ").Line(file.Path).Line();
            output.Append(fence).Line(FenceLanguage(file.Path));
            output.Append(file.Content);

            if (!file.Content.EndsWith('\n'))
            {
                output.Line();
            }

            output.Line(fence).Line();
        }
    }

    private static void AppendCandidates(StringBuilder output, ContentSet content, DiagnosisTask task, TaxonomyText? labels)
    {
        var candidates = Candidates(content, task);

        output.Line("## Step 1 — what do you see?").Line();
        output.Line(task.Difficulty == TaskDifficulty.Easy
            ? FormattableString.Invariant($"The shortlist of this easy task: {candidates.Count} cards.")
            : FormattableString.Invariant($"The whole problem catalogue: {candidates.Count} cards."));
        output.Line();

        foreach (var group in candidates.GroupBy(card => card.Class))
        {
            var name = labels?.Classes.GetValueOrDefault(group.Key)?.Name ?? group.Key;

            output.Append("### ").Line(name).Line();

            foreach (var card in group)
            {
                var cardText = card.Texts.GetValueOrDefault(ContentRules.DefaultLocale);

                if (cardText is not null)
                {
                    output.Line(FormattableString.Invariant($"- `{card.Slug}` — **{cardText.Name}**. {cardText.Summary}"));
                }
            }

            output.Line();
        }
    }

    private static void AppendTreatments(StringBuilder output, Taxonomy taxonomy, TaxonomyText? labels)
    {
        output.Line("## Step 2 — what do you do with it here?").Line();

        foreach (var branch in taxonomy.Branches)
        {
            var text = labels?.Branches.GetValueOrDefault(branch.Id);

            output.Line(FormattableString.Invariant($"### `{branch.Id}` — {text?.Name ?? branch.Id}")).Line();

            foreach (var leaf in branch.Leaves)
            {
                output.Line(FormattableString.Invariant(
                    $"- `{branch.Id}.{leaf}` — {text?.Leaves.GetValueOrDefault(leaf) ?? leaf}"));
            }

            output.Line();
        }
    }

    private static string Difficulty(TaskDifficulty difficulty) => difficulty switch
    {
        TaskDifficulty.Easy => "easy",
        TaskDifficulty.Medium => "medium",
        TaskDifficulty.Hard => "hard",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };

    // '\n' on every platform, so the view is the same text wherever it is rendered.
    private static StringBuilder Line(this StringBuilder output, string text = "") => output.Append(text).Append('\n');

    // A fence longer than any run of backticks in the file, so no file can close it early.
    private static string Fence(string content)
    {
        var longest = 0;
        var run = 0;

        foreach (var character in content)
        {
            run = character == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return new string('`', Math.Max(3, longest + 1));
    }

    private static string FenceLanguage(string path) => Path.GetExtension(path) switch
    {
        ".py" => "python",
        ".toml" => "toml",
        ".yaml" or ".yml" => "yaml",
        ".json" => "json",
        ".md" => "markdown",
        _ => string.Empty,
    };
}
