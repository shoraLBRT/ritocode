using Microsoft.AspNetCore.Authorization;

namespace Ritocode.Shared.Identity;

/// <summary>
/// The admin area's authorisation policy (docs/SPEC.md §6.2). An endpoint of the area says
/// <c>RequireAuthorization(AdminPolicy.Name)</c> where it is mapped.
/// </summary>
/// <remarks>
/// <para>
/// Who is an admin is the Users module's to answer — admins are named in its configuration by e-mail
/// (SPEC §9.1) — so the Users module registers the handler for <see cref="AdminRequirement"/>. The
/// policy and the requirement live here because endpoints in more than one module carry them, and a
/// module never references another.
/// </para>
/// <para>
/// A caller who is signed in but not an admin is refused with the same 404 an unknown address under
/// the API answers, so the area does not confirm it exists: see <see cref="AdminRefusalAsNotFound"/>.
/// An anonymous caller is refused with a 401, as for any address under the API.
/// </para>
/// </remarks>
public static class AdminPolicy
{
    public const string Name = "admin";

    /// <summary>A signed-in user whom the Users module names an admin.</summary>
    public static AuthorizationPolicy Build() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminRequirement())
            .Build();
}

/// <summary>The caller is an admin. Met by the Users module's handler, which owns the list of admins.</summary>
public sealed class AdminRequirement : IAuthorizationRequirement;
