using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Session;

namespace Ritocode.Modules.Auth.SignIn;

/// <summary>
/// GitHub and Google as ASP.NET's OAuth handler, which keeps the state parameter, its correlation
/// cookie and PKCE (docs/SPEC.md §10.1). The handler is asked for the round trip only: when the
/// provider returns, the identity is linked (<see cref="AccountLinker"/>), a session is started with
/// <see cref="ISessionIssuer"/> and its cookies written, and the browser goes back to the local path
/// it came from — no cookie scheme ever signs the handler's ticket in.
/// </summary>
internal static partial class OAuthProviders
{
    /// <summary>The route values of <c>/auth/login/{provider}</c>, which are also the scheme names.</summary>
    public const string GitHub = "github";

    public const string Google = "google";

    private const string IdentityItem = "ritocode:external-identity";

    public static IReadOnlyList<string> All { get; } = [GitHub, Google];

    public static string ConfigurationSection(string scheme) => scheme == GitHub ? "Auth:GitHub" : "Auth:Google";

    /// <summary>
    /// Registers each provider that has a client configured. An unconfigured one is left out entirely:
    /// the OAuth handler refuses to start without a client id, and every request runs past it.
    /// </summary>
    public static void Add(AuthenticationBuilder authentication, IConfiguration configuration)
    {
        foreach (var scheme in All)
        {
            var client = configuration.GetSection(ConfigurationSection(scheme)).Get<OAuthProviderOptions>() ?? new OAuthProviderOptions();

            if (!client.IsConfigured)
            {
                continue;
            }

            authentication.AddOAuth(scheme, options =>
            {
                options.ClientId = client.ClientId;
                options.ClientSecret = client.ClientSecret;
                options.CallbackPath = $"/auth/callback/{scheme}";
                options.UsePkce = true;
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = ReadIdentityAsync,
                    OnTicketReceived = CompleteSignInAsync,
                    OnRemoteFailure = FailAsync,
                };

                if (scheme == GitHub)
                {
                    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                    options.UserInformationEndpoint = "https://api.github.com/user";
                    options.Scope.Add("read:user");
                    options.Scope.Add("user:email");
                }
                else
                {
                    options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
                    options.TokenEndpoint = "https://oauth2.googleapis.com/token";
                    options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
                    options.Scope.Add("openid");
                    options.Scope.Add("email");
                }
            });
        }
    }

    /// <summary>Whether <paramref name="provider"/> names a registered scheme, and which.</summary>
    public static async Task<string?> FindSchemeAsync(IAuthenticationSchemeProvider schemes, string provider)
    {
        var name = provider.ToLowerInvariant();
        return All.Contains(name) && await schemes.GetSchemeAsync(name).ConfigureAwait(false) is not null ? name : null;
    }

    private static async Task ReadIdentityAsync(OAuthCreatingTicketContext context)
    {
        var cancellationToken = context.HttpContext.RequestAborted;

        using var user = await GetJsonAsync(context, context.Options.UserInformationEndpoint, cancellationToken).ConfigureAwait(false);

        ExternalIdentity identity;
        if (context.Scheme.Name == GitHub)
        {
            // The address and whether it is verified are only on /user/emails.
            using var emails = await GetJsonAsync(context, context.Options.UserInformationEndpoint.TrimEnd('/') + "/emails", cancellationToken).ConfigureAwait(false);
            identity = ExternalIdentity.FromGitHub(user.RootElement, emails.RootElement);
        }
        else
        {
            identity = ExternalIdentity.FromGoogle(user.RootElement);
        }

        context.HttpContext.Items[IdentityItem] = identity;
    }

    private static async Task CompleteSignInAsync(TicketReceivedContext context)
    {
        context.HandleResponse();

        var http = context.HttpContext;
        var appOrigin = http.RequestServices.GetRequiredService<IOptions<SignInOptions>>().Value.AppOrigin;
        var returnUrl = context.ReturnUri ?? ReturnUrl.Default;

        if (http.Items[IdentityItem] is not ExternalIdentity identity)
        {
            throw new InvalidOperationException("The provider's identity was not read before its ticket arrived.");
        }

        var logger = Logger(http);
        var linker = http.RequestServices.GetRequiredService<AccountLinker>();
        var result = await linker.LinkAsync(identity, http.RequestAborted).ConfigureAwait(false);

        if (result.UserId is not { } userId)
        {
            var code = result.Refusal == LinkRefusal.EmailUnverified ? "email_unverified" : "provider_already_linked";
            LogRefused(logger, identity.Provider, code);
            http.Response.Redirect(ReturnUrl.Resolve(appOrigin, returnUrl, code));
            return;
        }

        var session = await http.RequestServices.GetRequiredService<ISessionIssuer>()
            .StartAsync(userId, http.RequestAborted)
            .ConfigureAwait(false);

        SessionCookies.Write(http.Response, session);
        LogSignedIn(logger, identity.Provider, userId);
        http.Response.Redirect(ReturnUrl.Resolve(appOrigin, returnUrl));
    }

    /// <summary>
    /// The provider refused, the person cancelled, or the state did not match: back to where they came
    /// from, if the protected state still says where, with a code the page can explain.
    /// </summary>
    private static Task FailAsync(RemoteFailureContext context)
    {
        context.HandleResponse();

        var http = context.HttpContext;
        var appOrigin = http.RequestServices.GetRequiredService<IOptions<SignInOptions>>().Value.AppOrigin;

        var logger = Logger(http);
        LogFailed(logger, context.Scheme.Name, context.Failure);
        http.Response.Redirect(ReturnUrl.Resolve(appOrigin, context.Properties?.RedirectUri ?? ReturnUrl.Default, "provider_failed"));

        return Task.CompletedTask;
    }

    private static async Task<JsonDocument> GetJsonAsync(OAuthCreatingTicketContext context, string address, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // GitHub's API refuses a request without one.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Ritocode", "1.0"));

        using var response = await context.Backchannel.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static ILogger Logger(HttpContext http) =>
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OAuthProviders).FullName!);

    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Signed in with {Provider} as user {UserId}")]
    private static partial void LogSignedIn(ILogger logger, IdentityProvider provider, Guid userId);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Sign-in with {Provider} reached no account: {Code}")]
    private static partial void LogRefused(ILogger logger, IdentityProvider provider, string code);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning, Message = "Sign-in with {Scheme} failed at the provider")]
    private static partial void LogFailed(ILogger logger, string scheme, Exception? exception);
}
