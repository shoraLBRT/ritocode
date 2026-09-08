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

        var published = Published();
        var totalItems = await published.LongCountAsync(cancellationToken);

        // Offset is a long because a page number is unbounded, and Skip is not. Past the end there
        // is nothing to fetch anyway, so the comparison doubles as the guard against the cast.
        if (request.Offset >= totalItems)
        {
            return Page<CatalogProblem>.From([], request, totalItems);
        }

        var items = await Ordered(published)
            .Skip((int)request.Offset)
            .Take(request.PageSize)
            .Select(row => new CatalogProblem(
                row.Problem.Id,
                row.Problem.Slug,
                row.Problem.Title,
                row.Problem.Difficulty,
                row.Problem.Tags,
                row.Version!.Id,
                row.Version.Version,
                row.Version.PublishedAt!.Value))
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

        var detail = await Published()
            .Where(row => row.Problem.Slug == normalized)
            .Select(row => new CatalogProblemDetail(
                row.Problem.Id,
                row.Problem.Slug,
                row.Problem.Title,
                row.Problem.Difficulty,
                row.Problem.Tags,
                row.Version!.Id,
                row.Version.Version,
                row.Version.PublishedAt!.Value,
                row.Problem.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return detail is null ? NotFound(slug) : detail;
    }

    private static AppError NotFound(string? slug) =>
        AppError.NotFound(NotFoundCode, $"No published problem is addressed by '{slug}'.");

    /// <summary>
    /// Every problem that has at least one published version, paired with the highest-numbered one.
    /// Draft versions are invisible: the catalog resolves published versions only, per
    /// docs/DOMAIN_MODEL.md.
    /// </summary>
    private IQueryable<CatalogRow> Published() =>
        context.Problems
            .AsNoTracking()
            .Select(problem => new CatalogRow(
                problem,
                context.ProblemVersions
                    .Where(version => version.ProblemId == problem.Id && version.PublishedAt != null)
                    .OrderByDescending(version => version.Version)
                    .FirstOrDefault()))
            .Where(row => row.Version != null);

    /// <summary>
    /// Newest first. <c>Id</c> breaks ties rather than decorating the ordering: it is a UUIDv7, so
    /// it agrees with <c>CreatedAt</c> and makes the sort total — without which two problems
    /// created in the same instant could swap places between two requests for the same page.
    /// </summary>
    private static IOrderedQueryable<CatalogRow> Ordered(IQueryable<CatalogRow> rows) =>
        rows.OrderByDescending(row => row.Problem.CreatedAt)
            .ThenByDescending(row => row.Problem.Id);

    private sealed record CatalogRow(Problem Problem, ProblemVersion? Version);
}
