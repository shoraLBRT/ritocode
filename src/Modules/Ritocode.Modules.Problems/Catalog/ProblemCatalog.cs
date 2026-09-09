using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Problems.Catalog;

/// <summary>The catalog over the module's own schema. See <see cref="IProblemCatalog"/>.</summary>
public sealed class ProblemCatalog(ProblemsDbContext context) : IProblemCatalog
{
    /// <summary>Stable code clients branch on, per ADR 0003.</summary>
    public const string NotFoundCode = "problem_not_found";

    public async Task<Page<CatalogProblem>> ListAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var published = LatestPublished();
        var totalItems = await published.LongCountAsync(cancellationToken);

        // Offset is a long because a page number is unbounded, and Skip is not. Past the end there
        // is nothing to fetch anyway, so the comparison doubles as the guard against the cast.
        if (request.Offset >= totalItems)
        {
            return Page<CatalogProblem>.From([], request, totalItems);
        }

        var items = await published
            // Newest first. ProblemId breaks ties rather than decorating the ordering: it is a
            // UUIDv7, so it agrees with CreatedAt and makes the sort total — without which two
            // problems created in the same instant could swap places between two requests for the
            // same page.
            .OrderByDescending(version => version.Problem!.CreatedAt)
            .ThenByDescending(version => version.ProblemId)
            .Skip((int)request.Offset)
            .Take(request.PageSize)
            .Select(version => new CatalogProblem(
                version.ProblemId,
                version.Problem!.Slug,
                version.Problem.Title,
                version.Problem.Difficulty,
                version.Problem.Tags,
                version.Id,
                version.Version,
                version.PublishedAt!.Value))
            .ToListAsync(cancellationToken);

        return Page<CatalogProblem>.From(items, request, totalItems);
    }

    public async Task<Result<CatalogProblemDetail>> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        // Slugs are stored lower-cased and trimmed, so the same normalisation is what makes an
        // address from a link, a bookmark or a hand-typed URL resolve to the same row.
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();

        // Longer than the column can hold matches nothing by definition; answering without a round
        // trip keeps an oversized path from being a database question.
        if (normalized.Length is 0 or > Problem.SlugMaxLength)
        {
            return NotFound(slug);
        }

        var detail = await LatestPublished()
            .Where(version => version.Problem!.Slug == normalized)
            .Select(version => new CatalogProblemDetail(
                version.ProblemId,
                version.Problem!.Slug,
                version.Problem.Title,
                version.Problem.Difficulty,
                version.Problem.Tags,
                version.Id,
                version.Version,
                version.PublishedAt!.Value,
                version.Problem.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return detail is null ? NotFound(slug) : detail;
    }

    private static AppError NotFound(string? slug) =>
        AppError.NotFound(NotFoundCode, $"No published problem is addressed by '{slug}'.");

    /// <summary>
    /// The highest published version of every problem that has one — one row per catalog entry.
    /// Draft versions are invisible, and a draft numbered above the published one does not hide it:
    /// the catalog resolves published versions only, per docs/DOMAIN_MODEL.md.
    /// </summary>
    /// <remarks>
    /// Written from <c>ProblemVersions</c> rather than from <c>Problems</c> with the version
    /// attached, because the second shape does not survive translation: projecting a problem and a
    /// subquery into a type and then filtering on that type's member leaves EF unable to map the
    /// member back to the constructor argument, and the query fails at runtime rather than at
    /// compile time. Starting from the version keeps the whole thing one correlated
    /// <c>MAX</c> and the navigation to <c>Problem</c> a plain join.
    /// </remarks>
    private IQueryable<ProblemVersion> LatestPublished() =>
        context.ProblemVersions
            .AsNoTracking()
            .Where(version => version.PublishedAt != null
                && version.Version == context.ProblemVersions
                    .Where(other => other.ProblemId == version.ProblemId && other.PublishedAt != null)
                    .Max(other => other.Version));
}
