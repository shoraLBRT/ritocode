using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Modules.Attempts.Studying;

/// <summary>
/// <c>GET /api/v1/me/studied</c>, <c>PUT</c> and <c>DELETE /api/v1/me/studied/{card}</c> (docs/SPEC.md
/// §9.3): the caller's marks «Изучено». Closed to an anonymous caller by the host's fallback policy.
/// </summary>
internal static class StudyEndpoints
{
    public static IEndpointRouteBuilder MapStudyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me/studied", async (IStudyMarks marks, ICurrentUser currentUser, CancellationToken cancellationToken) =>
                Results.Ok(await marks.ListAsync(currentUser.RequireId(), cancellationToken)))
            .WithName("GetStudiedCards")
            .WithTags("Attempts");

        endpoints.MapPut("/me/studied/{card}", MarkAsync)
            .WithName("MarkCardStudied")
            .WithTags("Attempts");

        endpoints.MapDelete("/me/studied/{card}", UnmarkAsync)
            .WithName("UnmarkCardStudied")
            .WithTags("Attempts");

        return endpoints;
    }

    /// <summary>200 with the mark, the first one's date if the card was already marked.</summary>
    private static async Task<IResult> MarkAsync(
        string card,
        IStudyMarks marks,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await marks.MarkAsync(currentUser.RequireId(), card, cancellationToken);

        return result.Match(mark => Results.Ok(mark), error => ApiProblem.ToResult(error, context));
    }

    /// <summary>204, whether or not the card was marked.</summary>
    private static async Task<IResult> UnmarkAsync(string card, IStudyMarks marks, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        await marks.UnmarkAsync(currentUser.RequireId(), card, cancellationToken);

        return Results.NoContent();
    }
}
