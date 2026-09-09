using Ritocode.Modules.Problems.Domain;

namespace Ritocode.Modules.Problems.Catalog;

/// <summary>
/// One row of the catalog list: a problem, together with the published version a workspace would
/// be created from.
/// </summary>
/// <remarks>
/// A read model of its own rather than the <see cref="Problem"/> entity. Serialising an entity
/// would put its navigation properties and its draft versions on the wire, and would tie the
/// response shape to the next schema change.
/// </remarks>
/// <param name="Id">The problem's id. Opaque to clients, per ADR 0003.</param>
/// <param name="Slug">Stable identifier used in catalog URLs; survives a title change.</param>
/// <param name="ProblemVersionId">
/// The version a workspace is created from (#10). Carried in the list as well as the detail so a
/// client never has to guess which version a row refers to.
/// </param>
public sealed record CatalogProblem(
    Guid Id,
    string Slug,
    string Title,
    Difficulty Difficulty,
    IReadOnlyList<string> Tags,
    Guid ProblemVersionId,
    int Version,
    DateTimeOffset PublishedAt);

/// <summary>
/// The problem detail screen's payload: everything in <see cref="CatalogProblem"/> plus the
/// Markdown description.
/// </summary>
/// <remarks>
/// Flat rather than nested around a <see cref="CatalogProblem"/>, so the detail response is the
/// list response with one more field instead of a differently shaped object the client has to
/// unwrap. Hints are not here: they live in the package manifest inside the bundle, and the
/// screen that reveals them one at a time is stage 6.
/// </remarks>
public sealed record CatalogProblemDetail(
    Guid Id,
    string Slug,
    string Title,
    Difficulty Difficulty,
    IReadOnlyList<string> Tags,
    Guid ProblemVersionId,
    int Version,
    DateTimeOffset PublishedAt,
    string Description);
