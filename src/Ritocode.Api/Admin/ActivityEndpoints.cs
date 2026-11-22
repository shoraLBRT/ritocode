using System.Globalization;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;
using Ritocode.Shared.Identity;

namespace Ritocode.Api.Admin;

/// <summary>
/// The admin area's usage over time (#304) under <c>/api/v1/admin/stats/activity</c>, for admins only: a
/// signed-in non-admin gets the 404 of an unknown address (<see cref="AdminPolicy"/>). Host-level, as it
/// composes the Attempts and Users modules' answers and belongs to neither.
/// </summary>
public static class ActivityEndpoints
{
    /// <summary>The longest range one request may ask for, in days.</summary>
    public const int MaxDays = 366;

    /// <summary>The range when none is given: the last 30 days, today included.</summary>
    public const int DefaultDays = 30;

    public static IServiceCollection AddActivityStats(this IServiceCollection services)
    {
        services.AddScoped<ActivityReader>();

        return services;
    }

    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/admin/stats/activity", ReadAsync)
            .WithName("AdminReadActivity")
            .WithTags("Admin")
            .RequireAuthorization(AdminPolicy.Name);

        return endpoints;
    }

    /// <summary>
    /// <c>?from=&amp;to=</c> as UTC dates (<c>yyyy-MM-dd</c>, both included; the last 30 days by default) and
    /// <c>?bucket=day</c> (the default) or <c>week</c>.
    /// </summary>
    private static async Task<IResult> ReadAsync(
        ActivityReader reader,
        TimeProvider clock,
        HttpContext context,
        string? from,
        string? to,
        string? bucket,
        CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var (first, last) = ParseRange(from, to, today, fields);

        var size = bucket?.ToUpperInvariant() switch
        {
            null or "DAY" => 1,
            "WEEK" => 7,
            _ => 0,
        };

        if (size == 0)
        {
            fields["bucket"] = ["Must be 'day' or 'week'."];
        }

        if (fields.Count > 0)
        {
            return ApiProblem.ToResult(AppError.Validation("Invalid query parameters.", fields), context);
        }

        return Results.Ok(await reader.ReadAsync(first, last, size, cancellationToken));
    }

    /// <summary>
    /// <c>from</c>..<c>to</c> as UTC dates (<c>yyyy-MM-dd</c>, both included): the last <see cref="DefaultDays"/>
    /// days ending today by default, at most <see cref="MaxDays"/> long. A wrong value is added to
    /// <paramref name="fields"/>. Shared with the traffic panel (#306), so both read a range alike.
    /// </summary>
    internal static (DateOnly First, DateOnly Last) ParseRange(string? from, string? to, DateOnly today, Dictionary<string, string[]> fields)
    {
        var last = ParseDate(to, today, "to", fields);
        var first = ParseDate(from, last.AddDays(-(DefaultDays - 1)), "from", fields);

        if (fields.Count == 0)
        {
            if (first > last)
            {
                fields["from"] = ["Must not be after 'to'."];
            }
            else if (last.DayNumber - first.DayNumber + 1 > MaxDays)
            {
                fields["from"] = [$"The range must not be longer than {MaxDays} days."];
            }
        }

        return (first, last);
    }

    private static DateOnly ParseDate(string? value, DateOnly fallback, string name, Dictionary<string, string[]> fields)
    {
        if (value is null)
        {
            return fallback;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        fields[name] = ["Must be a date as yyyy-MM-dd."];
        return fallback;
    }
}
