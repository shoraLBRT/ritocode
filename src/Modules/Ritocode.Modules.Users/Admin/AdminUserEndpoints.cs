using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Users.Persistence;
using Ritocode.Shared.Contracts.Attempts;
using Ritocode.Shared.Contracts.Auth;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;
using Ritocode.Shared.Paging;

namespace Ritocode.Modules.Users.Admin;

/// <summary>One row of the admin area's list of users (docs/SPEC.md §6.2).</summary>
/// <param name="Providers">The providers the user signed in with, as the sign-in addresses name them; empty for the development identity.</param>
/// <param name="Attempts">Every attempt started, submitted or not.</param>
/// <param name="TasksSolved">Tasks with at least one submitted attempt.</param>
public sealed record AdminUserView(
    Guid Id,
    string Email,
    string Username,
    IReadOnlyList<string> Providers,
    DateTimeOffset RegisteredAt,
    int Attempts,
    int TasksSolved);

/// <summary><c>GET /api/v1/admin/users</c>, for admins only: every user, newest first, a page at a time.</summary>
internal static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/users", ListAsync)
            .WithName("AdminListUsers")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        UsersDbContext users,
        ILinkedProviderLookup providers,
        IAttemptTallyLookup tallies,
        HttpContext context,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Create(page, pageSize);

        if (!request.IsSuccess)
        {
            return ApiProblem.ToResult(request.Error, context);
        }

        var total = await users.Users.LongCountAsync(cancellationToken);

        var rows = await users.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip((int)request.Value.Offset)
            .Take(request.Value.PageSize)
            .ToListAsync(cancellationToken);

        var ids = rows.Select(user => user.Id).ToList();
        var linked = await providers.FindProvidersAsync(ids, cancellationToken);
        var counted = await tallies.TallyAsync(ids, cancellationToken);

        var items = rows
            .Select(user =>
            {
                var tally = counted.GetValueOrDefault(user.Id);
                return new AdminUserView(
                    user.Id,
                    user.Email,
                    user.Username,
                    linked.GetValueOrDefault(user.Id) ?? [],
                    user.CreatedAt,
                    tally?.Attempts ?? 0,
                    tally?.TasksSolved ?? 0);
            })
            .ToList();

        return Results.Ok(Page<AdminUserView>.From(items, request.Value, total));
    }
}
