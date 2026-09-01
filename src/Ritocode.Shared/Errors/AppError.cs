namespace Ritocode.Shared.Errors;

/// <summary>
/// A failure described in domain terms. <see cref="Code"/> is the stable, machine-readable
/// identifier clients branch on; <see cref="Message"/> is human-facing and may change freely.
/// </summary>
/// <param name="Type">Classification used to derive the HTTP status code.</param>
/// <param name="Code">Stable snake_case identifier, for example <c>workspace_not_found</c>.</param>
/// <param name="Message">Human-readable description. Must not leak internal details.</param>
/// <param name="Fields">Per-field messages, populated for <see cref="ErrorType.Validation"/>.</param>
public sealed record AppError(
    ErrorType Type,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static AppError Validation(string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(ErrorType.Validation, "validation_failed", message, fields);

    public static AppError NotFound(string code, string message) =>
        new(ErrorType.NotFound, code, message);

    public static AppError Conflict(string code, string message) =>
        new(ErrorType.Conflict, code, message);

    public static AppError Forbidden(string code, string message) =>
        new(ErrorType.Forbidden, code, message);

    public static AppError Unauthenticated(string code = "unauthenticated", string message = "Authentication is required.") =>
        new(ErrorType.Unauthenticated, code, message);
}
