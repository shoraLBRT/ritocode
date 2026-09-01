using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Ritocode.Shared.Validation;

public static class ValidationFilterExtensions
{
    /// <summary>
    /// Validates the bound <typeparamref name="TRequest"/> argument before the handler runs,
    /// returning the unified 400 error body when it fails, and advertises that 400 in OpenAPI.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddEndpointFilter<ValidationEndpointFilter<TRequest>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
