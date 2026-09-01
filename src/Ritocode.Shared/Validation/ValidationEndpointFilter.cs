using FluentValidation;
using Microsoft.AspNetCore.Http;
using Ritocode.Shared.Errors;
using Ritocode.Shared.Http;

namespace Ritocode.Shared.Validation;

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> against the request body before the handler
/// executes, so handlers only ever see valid input. Attach with
/// <see cref="ValidationFilterExtensions.WithValidation{TRequest}"/>.
/// </summary>
public sealed class ValidationEndpointFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            // The endpoint declares validation for a parameter it does not take. That is a wiring
            // bug rather than a client error, so fail loudly instead of silently passing through.
            throw new InvalidOperationException(
                $"Endpoint declares validation for '{typeof(TRequest).Name}' but no argument of that type was bound.");
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var fields = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);

        return ApiProblem.ToResult(
            AppError.Validation("One or more fields are invalid.", fields),
            context.HttpContext);
    }

    /// <summary>
    /// FluentValidation reports CLR property names; the API speaks camelCase, so field keys in the
    /// error payload match the JSON the client sent.
    /// </summary>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 0 && char.IsUpper(segment[0]))
            {
                segments[i] = char.ToLowerInvariant(segment[0]) + segment[1..];
            }
        }

        return string.Join('.', segments);
    }
}
