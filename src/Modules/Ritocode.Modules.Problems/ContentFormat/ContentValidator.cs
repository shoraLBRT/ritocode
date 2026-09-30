namespace Ritocode.Modules.Problems.ContentFormat;

/// <summary>
/// The rules that span more than one file: a card against the taxonomy, a task against the cards,
/// the leaves and its material, and the warnings an author should see but that never block.
/// </summary>
/// <remarks>
/// "A card removed while a published task still lists it" is, at this level, a finding naming a
/// card that does not exist. Ingest (#121) adds the database half: a card retired there.
/// </remarks>
internal static class ContentValidator
{
    public static void Validate(ContentSet content, ContentReport report)
    {
        var classes = content.Taxonomy.Classes.ToHashSet(StringComparer.Ordinal);
        var leaves = content.Taxonomy.LeafIds.ToHashSet(StringComparer.Ordinal);
        var cards = content.Cards.Select(card => card.Slug).ToHashSet(StringComparer.Ordinal);
        var materials = content.Materials.ToDictionary(material => material.Slug, StringComparer.Ordinal);

        foreach (var card in content.Cards)
        {
            ValidateCard(card, classes, report);
        }

        foreach (var task in content.Tasks)
        {
            ValidateTask(task, cards, leaves, materials, content.Cards.Count, report);
        }

        foreach (var material in content.Materials)
        {
            ValidateMaterial(material, content.Tasks, report);
        }
    }

    private static void ValidateCard(ProblemCard card, HashSet<string> classes, ContentReport report)
    {
        var path = $"{ContentLoader.CardsDirectory}/{card.Slug}/card.yaml";

        if (card.Class.Length > 0 && classes.Count > 0 && !classes.Contains(card.Class))
        {
            report.Error(path, $"'{card.Class}' is not a class in the taxonomy.");
        }

    }

    private static void ValidateTask(
        DiagnosisTask task,
        HashSet<string> cards,
        HashSet<string> leaves,
        Dictionary<string, Material> materials,
        int catalogueSize,
        ContentReport report)
    {
        var directory = $"{ContentLoader.TasksDirectory}/{task.Slug}";
        var path = $"{directory}/task.yaml";

        if (task.Material.Length > 0 && !materials.ContainsKey(task.Material))
        {
            report.Error(path, $"The material '{task.Material}' does not exist.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var finding in task.Findings)
        {
            if (finding.Card.Length == 0)
            {
                report.Error(path, "A finding has no 'card'.");
                continue;
            }

            if (!cards.Contains(finding.Card))
            {
                report.Error(path, $"The finding '{finding.Card}' names a card that does not exist.");
            }

            if (!seen.Add(finding.Card))
            {
                report.Error(path, $"The card '{finding.Card}' is listed twice.");
            }

            if (finding.Leaves.Count == 0)
            {
                report.Error(path, $"The finding '{finding.Card}' has no leaves; it needs at least one.");
            }

            foreach (var leaf in finding.Leaves.Where(leaf => !leaves.Contains(leaf)))
            {
                report.Error(path, $"The finding '{finding.Card}' names '{leaf}', which is not a leaf of the treatment tree.");
            }
        }

        foreach (var (locale, text) in task.Texts)
        {
            foreach (var card in text.Notes.Keys.Where(card => !seen.Contains(card)))
            {
                report.Error($"{directory}/{locale}.md", $"The note '### {card}' names a card that is not among the task's findings.");
            }
        }

        if (materials.TryGetValue(task.Material, out var material))
        {
            var (maxLines, maxFiles) = ContentRules.SizeBand(task.Difficulty);
            var difficulty = task.Difficulty.ToString().ToLowerInvariant();

            if (material.TotalLines > maxLines || material.Files.Count > maxFiles)
            {
                report.Warning(
                    path,
                    $"The material has {material.TotalLines} lines in {material.Files.Count} files; a {difficulty} task's band is at most {maxLines} lines in {maxFiles} files.");
            }
        }

        if (task.Difficulty == TaskDifficulty.Easy && catalogueSize - seen.Count < ContentRules.MinShortlistExtras)
        {
            report.Warning(
                path,
                $"The shortlist of an easy task needs at least {ContentRules.MinShortlistExtras} cards beside its findings; the catalogue has {catalogueSize - seen.Count}.");
        }
    }

    private static void ValidateMaterial(Material material, IReadOnlyList<DiagnosisTask> tasks, ContentReport report)
    {
        var directory = $"{ContentLoader.MaterialsDirectory}/{material.Slug}";

        if (material.Language.Length > 0 && !ContentRules.Languages.Contains(material.Language))
        {
            report.Error(
                $"{directory}/material.yaml",
                $"'{material.Language}' is not a supported language; the MVP accepts {string.Join(", ", ContentRules.Languages)}.");
        }

        foreach (var file in material.Files)
        {
            var lines = file.Content.Split('\n');
            var longLines = lines.Select((line, index) => (line, number: index + 1))
                .Where(pair => pair.line.Length > ContentRules.MaxLineLength)
                .ToList();

            if (longLines.Count > 0)
            {
                report.Warning(
                    $"{directory}/files/{file.Path}",
                    $"{longLines.Count} line(s) longer than {ContentRules.MaxLineLength} characters, the first at line {longLines[0].number}.");
            }
        }

        if (!tasks.Any(task => task.Material == material.Slug))
        {
            report.Warning($"{directory}/material.yaml", "No task uses this material.");
        }
    }
}
