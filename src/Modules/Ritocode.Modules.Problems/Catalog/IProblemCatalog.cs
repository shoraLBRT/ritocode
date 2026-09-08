using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Problems.Catalog;

/// <summary>
/// Reads of the published catalog. A problem appears here only once it has a published version,
/// and the version reported is always its highest published one.
/// </summary>
/// <remarks>
/// Search, tag and difficulty filters, facets and explicit version resolution are deferred with
/// the rest of issue #9 — see docs/SLICE_PLAN.md. They are additions to this interface rather than
/// replacements for it: <c>Page&lt;T&gt;</c> and <c>PageRequest</c> already fix the response shape,
/// which is the reduction ADR 0005 allows here.
/// </remarks>
public interface IProblemCatalog
{
    /// <summary>One page of published problems, newest first.</summary>
    Task<Page<CatalogProblem>> ListAsync(PageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The published problem addressed by <paramref name="slug"/>.
    /// </summary>
    /// <returns>
    /// A <c>problem_not_found</c> failure when no problem has that slug, and the same failure when
    /// one does but has no published version: a draft is not something the catalog may confirm the
    /// existence of.
    /// </returns>
    Task<Result<CatalogProblemDetail>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
