using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ritocode.Shared.Identity;

/// <summary>
/// Puts the caller's user id into the log scope, once authentication has run, so every later log line
/// of the request names whose it was. The id and nothing else: no e-mail, no name — personal data
/// stays out of the logs. An anonymous request adds nothing.
/// </summary>
public sealed class UserLogScopeMiddleware(RequestDelegate next, ILogger<UserLogScopeMiddleware> logger)
{
    /// <summary>Log scope property name, beside <see cref="Http.RequestId.LogPropertyName"/>.</summary>
    public const string LogPropertyName = "UserId";

    private static readonly Func<ILogger, Guid, IDisposable?> Scope =
        LoggerMessage.DefineScope<Guid>("UserId:{" + LogPropertyName + "}");

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentUser);

        if (currentUser.Id is not { } userId)
        {
            await next(context);
            return;
        }

        using (Scope(logger, userId))
        {
            await next(context);
        }
    }
}
