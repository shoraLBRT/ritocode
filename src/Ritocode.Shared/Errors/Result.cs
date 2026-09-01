using System.Diagnostics.CodeAnalysis;

namespace Ritocode.Shared.Errors;

/// <summary>
/// An operation outcome that is either a value or an <see cref="AppError"/>. Used for expected
/// failures (validation, not found, conflict) so they travel as return values instead of
/// exceptions; genuinely exceptional conditions still throw.
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Error = null;
    }

    private Result(AppError error)
    {
        _value = default;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public AppError? Error { get; }

    /// <summary>The produced value. Throws when the result is a failure; check <see cref="IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error!.Code}); no value is available.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(AppError error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(AppError error) => Failure(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(_value!) : onFailure(Error!);
    }
}
