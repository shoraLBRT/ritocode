using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Content.Catalogue;

/// <summary>
/// The four public reads of docs/SPEC.md §9.3. All anonymous: a signed-out visitor browses the
/// catalogue and solves a task, and only checking an answer needs an account.
/// </summary>
internal static class CatalogueEndpoints
{
    public static IEndpointRouteBuilder MapCatalogueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // The problem catalogue is one object, not a page: its screen shows every card grouped by
        // class with a search box and an anchor per card, which paging would break. Sixty cards.
        endpoints.MapGet("/problems", (IContentCatalogue catalogue, CancellationToken cancellationToken) =>
                catalogue.GetProblemsAsync(cancellationToken))
            .WithName("GetProblemCatalogue")
            .WithTags("Content")
            .AllowAnonymous();

        endpoints.MapGet("/treatments", (IContentCatalogue catalogue, CancellationToken cancellationToken) =>
                catalogue.GetTreatmentsAsync(cancellationToken))
            .WithName("GetTreatmentTree")
            .WithTags("Content")
            .AllowAnonymous();

        endpoints.MapGet("/tasks", ListTasksAsync)
            .WithName("ListTasks")
            .WithTags("Content")
            .AllowAnonymous();

        endpoints.MapGet("/tasks/{slug}", GetTaskAsync)
            .WithName("GetTask")
            .WithTags("Content")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ListTasksAsync(
        IContentCatalogue catalogue,
        ICurrentUser currentUser,
        HttpContext context,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        // Query parameters are not a request body, so the validation filter does not see them;
        // PageRequest holds the range rules and reports them in the same 400 shape.
        var request = PageRequest.Create(page, pageSize);

        return request.IsSuccess
            ? Results.Ok(await catalogue.ListTasksAsync(request.Value, currentUser.Id, cancellationToken))
            : ApiProblem.ToResult(request.Error!, context);
    }

    private static async Task<IResult> GetTaskAsync(
        IContentCatalogue catalogue,
        HttpContext context,
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await catalogue.GetTaskAsync(slug, cancellationToken);

        return result.Match(
            task => Results.Ok(task),
            error => ApiProblem.ToResult(error, context));
    }
}
