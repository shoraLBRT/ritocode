using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Identity;

namespace Ritocode.Shared.Tests.Identity;

/// <summary>
/// Reading the caller off the request. Everything here is about what counts as "no user", because
/// that is the answer a wrong one turns into somebody else's workspace.
/// </summary>
public sealed class HttpContextCurrentUserTests
{
    private static readonly Guid UserId = new("0199aa00-0000-7000-8000-0000000000ff");

    [Fact]
    public void AnAuthenticatedPrincipal_YieldsItsUserId()
    {
        var currentUser = CurrentUserFor(Authenticated(new Claim(RitocodeClaimTypes.UserId, UserId.ToString())));

        Assert.Equal(UserId, currentUser.Id);
        Assert.True(currentUser.IsAuthenticated());
        Assert.Equal(UserId, currentUser.RequireId());
    }

    [Fact]
    public void NoHttpContext_IsAnonymous()
    {
        // A hosted service or, from stage 4, the evaluation worker. There is no caller, and code
        // that runs in both places must be handed its user rather than read one from here.
        var currentUser = CurrentUserFor(principal: null);

        Assert.Null(currentUser.Id);
    }

    [Fact]
    public void AnUnauthenticatedPrincipal_IsAnonymous()
    {
        var currentUser = CurrentUserFor(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Null(currentUser.Id);
        Assert.False(currentUser.IsAuthenticated());
    }

    [Fact]
    public void AnAuthenticatedPrincipalWithNoUserIdClaim_IsAnonymous()
    {
        var currentUser = CurrentUserFor(Authenticated(new Claim(ClaimTypes.Name, "developer")));

        Assert.Null(currentUser.Id);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void AnUnusableUserIdClaim_IsAnonymousRatherThanAFailure(string value)
    {
        // Null rather than a throw: a malformed credential becomes a 401 at the authorisation check
        // or at RequireId, not a 500 at the first line of code that wanted a user. The empty GUID is
        // in here because it parses — it is the value a half-configured deployment produces.
        var currentUser = CurrentUserFor(Authenticated(new Claim(RitocodeClaimTypes.UserId, value)));

        Assert.Null(currentUser.Id);
    }

    [Fact]
    public void RequireId_OnAnAnonymousRequest_FailsAsUnauthenticated()
    {
        var currentUser = CurrentUserFor(new ClaimsPrincipal(new ClaimsIdentity()));

        var exception = Assert.Throws<AppException>(() => currentUser.RequireId());

        Assert.Equal(ErrorType.Unauthenticated, exception.Error.Type);
        Assert.Equal("unauthenticated", exception.Error.Code);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestScheme", ClaimTypes.Name, ClaimTypes.Role));

    /// <summary>
    /// Resolves <see cref="ICurrentUser"/> the way the host does, through
    /// <see cref="IdentityServiceCollectionExtensions.AddRitocodeIdentity"/>, so the registration is
    /// under test alongside the implementation rather than being assumed correct.
    /// </summary>
    private static ICurrentUser CurrentUserFor(ClaimsPrincipal? principal)
    {
        var services = new ServiceCollection()
            .AddRitocodeIdentity(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        if (principal is not null)
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext =
                new DefaultHttpContext { User = principal };
        }

        return services.CreateScope().ServiceProvider.GetRequiredService<ICurrentUser>();
    }
}
