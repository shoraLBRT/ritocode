using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Attempts.Admin;

/// <summary>
/// The admin area's signals and attempts (docs/SPEC.md §6.2, §9.3), under <c>/api/v1/admin</c>, for
/// admins only. A signed-in non-admin gets the 404 of an unknown address (<see cref="AdminPolicy"/>).
/// </summary>
internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        admin.MapGet("/signals", ListSignalsAsync).WithName("AdminListSignals");
        admin.MapPost("/signals/{id}/resolve", ResolveSignalAsync).WithName("AdminResolveSignal");
        admin.MapGet("/attempts", ListAttemptsAsync).WithName("AdminListAttempts");

        return endpoints;
    }

    /// <summary><c>?status=open</c> (the default) or <c>resolved</c>, newest first.</summary>
    private static async Task<IResult> ListSignalsAsync(
        IAdminReader reader,
        HttpContext context,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Create(page, pageSize);
        var fields = FieldsOf(request);

        if (!TryParse(status, SignalStatus.Open, out var filter))
        {
            fields["status"] = ["Must be 'open' or 'resolved'."];
        }

        return fields.Count > 0
            ? Invalid(fields, context)
            : Results.Ok(await reader.ListSignalsAsync(filter, request.Value, cancellationToken));
    }

    /// <summary>200 with the signal, resolved. Resolving one already resolved changes nothing.</summary>
    private static async Task<IResult> ResolveSignalAsync(
        string id,
        IAdminReader reader,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        // An id is an opaque string to a client (ADR 0003): a malformed one is a signal that does not exist.
        if (!Guid.TryParse(id, out var signalId))
        {
            return ApiProblem.ToResult(AdminReader.SignalNotFound(), context);
        }

        var result = await reader.ResolveSignalAsync(signalId, cancellationToken);

        return result.Match(signal => Results.Ok(signal), error => ApiProblem.ToResult(error, context));
    }

    /// <summary><c>?status=all</c> (the default), <c>open</c> or <c>submitted</c>, and <c>?user=</c> for one learner; newest first.</summary>
    private static async Task<IResult> ListAttemptsAsync(
        IAdminReader reader,
        HttpContext context,
        string? status,
        string? user,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Create(page, pageSize);
        var fields = FieldsOf(request);

        if (!TryParse(status, AttemptStatus.All, out var filter))
        {
            fields["status"] = ["Must be 'all', 'open' or 'submitted'."];
        }

        Guid? userId = null;
        if (user is not null)
        {
            if (Guid.TryParse(user, out var parsed))
            {
                userId = parsed;
            }
            else
            {
                fields["user"] = ["Must be a user id."];
            }
        }

        return fields.Count > 0
            ? Invalid(fields, context)
            : Results.Ok(await reader.ListAttemptsAsync(filter, userId, request.Value, cancellationToken));
    }

    /// <summary>A status by its name, ignoring case; <paramref name="fallback"/> when absent. Numbers are not names.</summary>
    private static bool TryParse<TStatus>(string? value, TStatus fallback, out TStatus status)
        where TStatus : struct, Enum
    {
        status = fallback;

        if (value is null)
        {
            return true;
        }

        var match = Enum.GetNames<TStatus>().FirstOrDefault(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        status = Enum.Parse<TStatus>(match);
        return true;
    }

    /// <summary>The page parameters' own messages, to which the filters add theirs, so one answer names every bad parameter.</summary>
    private static Dictionary<string, string[]> FieldsOf(Result<PageRequest> request) =>
        request.IsSuccess || request.Error?.Fields is null
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(request.Error.Fields, StringComparer.Ordinal);

    private static IResult Invalid(Dictionary<string, string[]> fields, HttpContext context) =>
        ApiProblem.ToResult(AppError.Validation("Invalid query parameters.", fields), context);
}
