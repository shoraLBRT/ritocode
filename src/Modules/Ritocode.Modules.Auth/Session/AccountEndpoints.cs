using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Ritocode.Shared.Contracts.Users;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.PersonalData;

namespace Ritocode.Modules.Auth.Session;

/// <summary>Everything the site keeps about the caller, module by module (#299).</summary>
/// <param name="ExportedAt">When the export was taken, in UTC.</param>
/// <param name="User">The caller's id.</param>
/// <param name="Modules">Each module's part, keyed by its name in camelCase: <c>users</c> first, then the rest by name.</param>
public sealed record AccountExport(DateTimeOffset ExportedAt, Guid User, IReadOnlyDictionary<string, object> Modules);

/// <summary>The body of <c>DELETE /api/v1/me</c>: the caller's username, typed to confirm.</summary>
public sealed record DeleteAccountRequest(string? Username);

/// <summary>
/// <c>GET /api/v1/me/export</c> and <c>DELETE /api/v1/me</c> (docs/SPEC.md §9.3, #299): the caller's
/// data handed over as a JSON file, and the account deleted with everything every module keeps about it.
/// </summary>
/// <remarks>
/// Here, beside <c>GET /me</c> and sign-out, because Auth owns the caller and ends their session. Neither
/// endpoint knows what a module keeps: each module that keeps a user's rows registers an
/// <see cref="IPersonalData"/> over its own schema, and these ask all of them (ADR 0021). Deleting is
/// state-changing, so with a session it needs the CSRF token, as every such request does.
/// </remarks>
internal static partial class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/me/export", ExportAsync)
            .WithName("ExportMe")
            .WithTags("Auth");

        endpoints.MapDelete("/me", DeleteAsync)
            .WithName("DeleteMe")
            .WithTags("Auth");

        return endpoints;
    }

    /// <summary>200 with the export, as an attachment named for the day it was taken.</summary>
    private static async Task<IResult> ExportAsync(
        ICurrentUser currentUser,
        IUserLookup users,
        IEnumerable<IPersonalData> holders,
        TimeProvider clock,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(currentUser.RequireId(), cancellationToken);

        if (user is null)
        {
            return NoUser(context);
        }

        var modules = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var holder in Ordered(holders, accountFirst: true))
        {
            modules[JsonNamingPolicy.CamelCase.ConvertName(holder.Module)] = await holder.ExportAsync(user.Id, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var day = now.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        context.Response.Headers.ContentDisposition = $"attachment; filename=\"ritocode-export-{day}.json\"";

        return Results.Ok(new AccountExport(now, user.Id, modules));
    }

    /// <summary>
    /// 204, the session's cookies cleared, once every module has erased the caller's rows — the account's
    /// own last. A username that is not the caller's is <c>400 validation_failed</c> and erases nothing.
    /// </summary>
    private static async Task<IResult> DeleteAsync(
        [FromBody] DeleteAccountRequest request,
        ICurrentUser currentUser,
        IUserLookup users,
        IEnumerable<IPersonalData> holders,
        ILoggerFactory loggers,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(currentUser.RequireId(), cancellationToken);

        if (user is null)
        {
            return NoUser(context);
        }

        if (!string.Equals(request.Username?.Trim(), user.Username, StringComparison.Ordinal))
        {
            return ApiProblem.ToResult(
                AppError.Validation(
                    "The username does not match the account.",
                    new Dictionary<string, string[]>(StringComparer.Ordinal) { ["username"] = ["Type the account's username to delete it."] }),
                context);
        }

        var erased = 0;

        foreach (var holder in Ordered(holders, accountFirst: false))
        {
            erased += await holder.EraseAsync(user.Id, cancellationToken);
        }

        var logger = loggers.CreateLogger(typeof(AccountEndpoints).FullName!);
        LogDeleted(logger, user.Id, erased);

        SessionCookies.Clear(context.Response);
        return Results.NoContent();
    }

    /// <summary>The account's own rows first for an export, which opens with who it is; last for erasing.</summary>
    private static IEnumerable<IPersonalData> Ordered(IEnumerable<IPersonalData> holders, bool accountFirst)
    {
        var byRole = accountFirst
            ? holders.OrderByDescending(holder => holder.HoldsTheAccount)
            : holders.OrderBy(holder => holder.HoldsTheAccount);

        return byRole.ThenBy(holder => holder.Module, StringComparer.Ordinal);
    }

    /// <summary>An identity that names no user is not a signed-in caller, as <c>GET /me</c> says.</summary>
    private static IResult NoUser(HttpContext context) =>
        ApiProblem.ToResult(AppError.Unauthenticated(message: "The authenticated identity does not name a user."), context);

    // The id only: the e-mail and username are what was just erased. A restore from a backup older than
    // the deletion finds the accounts to delete again by these lines (docs/RUNBOOK.md).
    [LoggerMessage(EventId = 1210, Level = LogLevel.Information, Message = "Deleted account {UserId} on its owner's request: {Rows} rows")]
    private static partial void LogDeleted(ILogger logger, Guid userId, int rows);
}
