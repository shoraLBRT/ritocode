using Ritocode.Modules.Content.Ingest;

namespace Ritocode.Modules.Content.Catalogue;

// The shapes the read APIs answer with (docs/SPEC.md §9.3). A card's weight and a task's answer key
// appear in none of them: weight is shown nowhere, and the key leaves the server only inside a
// submitted attempt.

/// <summary><c>GET /problems</c>: every card in full, and the classes they are grouped by.</summary>
public sealed record ProblemCatalogueView(IReadOnlyList<ClassView> Classes, IReadOnlyList<CardView> Cards);

public sealed record ClassView(string Id, string Name, string? Description);

public sealed record CardView(
    string Slug,
    string Class,
    string Name,
    string Summary,
    IReadOnlyList<string> Keywords,
    CardSectionsView Sections);

public sealed record CardSectionsView(
    string? Signs,
    string? WhyAiDoesIt,
    string? Cost,
    string? AcceptableWhen,
    string? Detection,
    string? Treatment,
    string? Sources,
    string? CounterArguments);

/// <summary><c>GET /treatments</c>: the treatment tree with its labels.</summary>
public sealed record TreatmentTreeView(IReadOnlyList<BranchView> Branches);

public sealed record BranchView(string Id, string Name, IReadOnlyList<LeafView> Leaves);

/// <summary>A leaf, addressed as <c>branch.leaf</c> — the identifier an answer names.</summary>
public sealed record LeafView(string Id, string Label);

/// <summary>
/// One row of <c>GET /tasks</c>. <see cref="Solved"/> — whether the caller has submitted an attempt at
/// it — is set in the catalogue for a signed-in caller, and <see langword="null"/> otherwise.
/// </summary>
public sealed record TaskSummaryView(string Slug, string Title, string Difficulty, bool? Solved = null);

/// <summary>
/// <c>GET /tasks/{slug}</c>: everything a learner needs to solve a task, and nothing that gives the
/// answer away — the cards offered carry a name, a summary and search keywords only.
/// </summary>
public sealed record TaskView(
    string Slug,
    string Title,
    string Difficulty,
    string Context,
    string Brief,
    MaterialView Material,
    IReadOnlyList<CandidateCardView> Cards,
    IReadOnlyList<TaskSummaryView> SameMaterial);

public sealed record MaterialView(IReadOnlyList<MaterialFileView> Files, MaterialOverview Overview);

public sealed record MaterialFileView(string Path, string Content);

public sealed record CandidateCardView(string Slug, string Class, string Name, string Summary, IReadOnlyList<string> Keywords);
