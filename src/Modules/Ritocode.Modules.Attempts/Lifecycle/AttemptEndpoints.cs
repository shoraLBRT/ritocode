using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Paging;
using Ritocode.Shared.Validation;

namespace Ritocode.Modules.Attempts.Lifecycle;

/// <summary>The attempt endpoints of docs/SPEC.md §9.3, under <c>/api/v1/attempts</c>.</summary>
/// <remarks>
/// None says anything about authorisation: the host's fallback policy closes them to an anonymous
/// caller, and every lookup behind them is by owner, so another user's attempt is a 404.
/// </remarks>
internal static class AttemptEndpoints
{
    private const string GetAttemptRouteName = "GetAttempt";

    public static IEndpointRouteBuilder MapAttemptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/", StartAsync)
            .WithName("StartAttempt")
            .WithTags("Attempts")
            .WithValidation<StartAttemptRequest>();

        endpoints.MapPatch("/{id}", RecordStepAsync)
            .WithName("RecordAttemptStep")
            .WithTags("Attempts")
            .WithValidation<RecordStepRequest>();

        endpoints.MapPost("/{id}/submit", SubmitAsync)
            .WithName("SubmitAttempt")
            .WithTags("Attempts")
            .WithValidation<SubmitAttemptRequest>();

        endpoints.MapGet("/{id}", GetAsync)
            .WithName(GetAttemptRouteName)
            .WithTags("Attempts");

        endpoints.MapGet("/", ListAsync)
            .WithName("ListAttempts")
            .WithTags("Attempts");

        return endpoints;
    }

    /// <summary>201 with a <c>Location</c>: every start is a new attempt.</summary>
    private static async Task<IResult> StartAsync(
        StartAttemptRequest request,
        IAttemptLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await lifecycle.StartAsync(currentUser.RequireId(), request.Task!, cancellationToken);

        return result.Match(
            attempt => Results.CreatedAtRoute(GetAttemptRouteName, new { id = attempt.Id }, attempt),
            error => ApiProblem.ToResult(error, context));
    }

    private static async Task<IResult> RecordStepAsync(
        string id,
        RecordStepRequest request,
        IAttemptLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        if (!Guid.TryParse(id, out var attemptId))
        {
            return NotFound(context);
        }

        // The validation filter has refused a step that is not one.
        if (!AttemptSteps.TryParse(request.Step, out var step))
        {
            throw new InvalidOperationException("A step the validation filter should have refused reached the handler.");
        }

        return Answer(await lifecycle.RecordStepAsync(userId, attemptId, step, cancellationToken), context);
    }

    private static async Task<IResult> SubmitAsync(
        string id,
        SubmitAttemptRequest request,
        IAttemptLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        if (!Guid.TryParse(id, out var attemptId))
        {
            return NotFound(context);
        }

        // The validation filter has refused a missing card, a missing leaf list and a repeated card.
        var answer = new DiagnosisAnswer([.. request.Picks!.Select(pick => new PickedCard(pick.Card!, [.. pick.Leaves!]))]);

        return Answer(await lifecycle.SubmitAsync(userId, attemptId, answer, cancellationToken), context);
    }

    private static async Task<IResult> GetAsync(
        string id,
        IAttemptLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // An id is an opaque string to a client (ADR 0003): a malformed one is an attempt this caller
        // does not have, in the same error body as any other.
        if (!Guid.TryParse(id, out var attemptId))
        {
            return NotFound(context);
        }

        return Answer(await lifecycle.GetAsync(userId, attemptId, cancellationToken), context);
    }

    private static async Task<IResult> ListAsync(
        IAttemptLifecycle lifecycle,
        ICurrentUser currentUser,
        HttpContext context,
        string? task,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireId();

        // Query parameters are not a body, so PageRequest reports its own range rules, as the catalogue does.
        var request = PageRequest.Create(page, pageSize);

        return request.IsSuccess
            ? Results.Ok(await lifecycle.ListAsync(userId, task, request.Value, cancellationToken))
            : ApiProblem.ToResult(request.Error, context);
    }

    private static IResult Answer(Shared.Errors.Result<AttemptView> result, HttpContext context) =>
        result.Match(attempt => Results.Ok(attempt), error => ApiProblem.ToResult(error, context));

    private static IResult NotFound(HttpContext context) => ApiProblem.ToResult(AttemptLifecycle.AttemptNotFound(), context);
}
