using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Http;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Problems.Catalog;

/// <summary>
/// The public catalog: <c>GET /api/v1/problems</c> and <c>GET /api/v1/problems/{slug}</c>.
/// </summary>
/// <remarks>
/// Anonymous, deliberately. Browsing the catalog is what someone does before they have an identity,
/// and issue #6's authentication middleware is written so that endpoints which should stay open say
/// so here rather than by not having been thought about.
/// </remarks>
internal static class ProblemCatalogEndpoints
{
    public static IEndpointRouteBuilder MapProblemCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", ListAsync)
            .WithName("ListProblems")
            .WithTags("Problems")
            .AllowAnonymous();

        endpoints.MapGet("/{slug}", GetAsync)
            .WithName("GetProblem")
            .WithTags("Problems")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        IProblemCatalog catalog,
        HttpContext context,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        // Query parameters are not a request body, so ValidationEndpointFilter does not see them;
        // PageRequest is where the range rules live and it reports them in the same 400 shape.
        var request = PageRequest.Create(page, pageSize);

        return request.IsSuccess
            ? Results.Ok(await catalog.ListAsync(request.Value, cancellationToken))
            : ApiProblem.ToResult(request.Error, context);
    }

    private static async Task<IResult> GetAsync(
        IProblemCatalog catalog,
        HttpContext context,
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await catalog.GetBySlugAsync(slug, cancellationToken);

        return result.Match(
            detail => Results.Ok(detail),
            error => ApiProblem.ToResult(error, context));
    }
}
