using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Modules.Auth.SignIn;

/// <summary>
/// <c>GET /auth/login/{provider}?returnUrl=</c>, outside the versioned API (docs/SPEC.md §9.3): sends
/// the browser to GitHub or Google. The provider returns to <c>/auth/callback/{provider}</c>, which
/// the OAuth handler answers before any endpoint (see <see cref="OAuthProviders"/>).
/// </summary>
internal static class LoginEndpoints
{
    public const string ProviderNotFoundCode = "provider_not_found";

    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder root)
    {
        root.MapGet("/auth/login/{provider}", LoginAsync)
            .WithName("Login")
            .WithTags("Auth")
            .AllowAnonymous();

        return root;
    }

    private static async Task<IResult> LoginAsync(
        string provider,
        string? returnUrl,
        HttpContext context,
        IAuthenticationSchemeProvider schemes)
    {
        if (await OAuthProviders.FindSchemeAsync(schemes, provider) is not { } scheme)
        {
            return ApiProblem.ToResult(AppError.NotFound(ProviderNotFoundCode, $"Signing in with '{provider}' is not offered."), context);
        }

        if (returnUrl is not null && !ReturnUrl.IsLocal(returnUrl))
        {
            return ApiProblem.ToResult(
                AppError.Validation(
                    "The return address must be a path on this site.",
                    new Dictionary<string, string[]> { ["returnUrl"] = ["Only a local path, such as /tasks/some-task, is accepted."] }),
                context);
        }

        return Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? ReturnUrl.Default }, [scheme]);
    }
}
