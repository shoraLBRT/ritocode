namespace Ritocode.Shared.Errors;

/// <summary>
/// Carries an <see cref="AppError"/> out of domain code. The API host translates it into the
/// unified error response; nothing else should catch it.
/// </summary>
public sealed class AppException : Exception
{
    public AppException(AppError error)
        : base(error.Message)
    {
        Error = error;
    }

    public AppException(AppError error, Exception innerException)
        : base(error.Message, innerException)
    {
        Error = error;
    }

    public AppError Error { get; }
}
