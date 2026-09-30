namespace Ritocode.Modules.Problems.ContentFormat;

/// <summary>
/// A content tree as docs/CONTENT_FORMAT.md defines it: the taxonomy, problem cards, materials and
/// tasks. Language-neutral data sits on the records themselves; everything localised is keyed by
/// locale, so a second locale is more entries rather than a different shape.
/// </summary>
public sealed record ContentSet(
    Taxonomy Taxonomy,
    IReadOnlyList<ProblemCard> Cards,
    IReadOnlyList<Material> Materials,
    IReadOnlyList<DiagnosisTask> Tasks);

/// <summary>The six classes and the treatment tree, with their labels per locale.</summary>
public sealed record Taxonomy(
    IReadOnlyList<string> Classes,
    IReadOnlyList<TreatmentBranch> Branches,
    IReadOnlyDictionary<string, TaxonomyText> Texts)
{
    public static Taxonomy Empty { get; } = new([], [], new Dictionary<string, TaxonomyText>());

    /// <summary>Every leaf, addressed as <c>branch.leaf</c>.</summary>
    public IEnumerable<string> LeafIds => Branches.SelectMany(branch => branch.Leaves.Select(leaf => $"{branch.Id}.{leaf}"));
}

public sealed record TreatmentBranch(string Id, IReadOnlyList<string> Leaves);

public sealed record TaxonomyText(
    IReadOnlyDictionary<string, LabelText> Classes,
    IReadOnlyDictionary<string, BranchText> Branches);

public sealed record LabelText(string Name, string? Description);

public sealed record BranchText(string Name, IReadOnlyDictionary<string, string> Leaves);

public sealed record ProblemCard(
    string Slug,
    string Class,
    int Weight,
    IReadOnlyDictionary<string, CardText> Texts);

public sealed record CardText(
    string Name,
    string Summary,
    IReadOnlyList<string> Keywords,
    IReadOnlyDictionary<CardSection, string> Sections);

/// <summary>The long fields of a card, one Markdown section each.</summary>
public enum CardSection
{
    Signs,
    WhyAiDoesIt,
    Cost,
    AcceptableWhen,
    Detection,
    Treatment,
    Sources,
    CounterArguments,
}

public sealed record Material(string Slug, string Language, string? Notes, IReadOnlyList<MaterialFile> Files)
{
    public int TotalLines => Files.Sum(file => file.LineCount);
}

/// <summary>One file of a material, its path relative to <c>files/</c> with <c>/</c> separators.</summary>
public sealed record MaterialFile(string Path, string Content)
{
    public int LineCount { get; } = CountLines(Content);

    private static int CountLines(string content)
    {
        if (content.Length == 0)
        {
            return 0;
        }

        var newlines = content.Count(character => character == '\n');

        return content.EndsWith('\n') ? newlines : newlines + 1;
    }
}

public enum TaskDifficulty
{
    Easy,
    Medium,
    Hard,
}

public sealed record DiagnosisTask(
    string Slug,
    string Material,
    TaskDifficulty Difficulty,
    IReadOnlyList<Finding> Findings,
    IReadOnlyDictionary<string, TaskText> Texts);

/// <summary>
/// One entry of a task's answer key: a card, and the leaves that are right for it in this context —
/// any one of them is right (docs/SPEC.md §5.2). An object rather than a bare card, so a location
/// can join it later without changing what a finding is.
/// </summary>
public sealed record Finding(string Card, IReadOnlyList<string> Leaves);

public sealed record TaskText(
    string Title,
    string Context,
    string Brief,
    IReadOnlyDictionary<string, string> Notes,
    string? Lesson);
