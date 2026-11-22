using System.Reflection;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Shared.Diagnostics;

namespace Ritocode.Modules.Errors.Recording;

/// <summary>Turns what each source says into an <see cref="ErrorReport"/> that fits the columns.</summary>
internal static class ErrorReports
{
    /// <summary>The API's own commit, as the SDK wrote it after the <c>+</c> of the informational version.</summary>
    private static readonly string? ServerCommit = CommitOf(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// The exception as it escaped: its type, its message, and its whole text (inner exceptions included)
    /// as the stack. The stack the fingerprint reads is the outer exception's own.
    /// </summary>
    public static (ErrorReport Report, string FingerprintStack) FromServer(ServerError error)
    {
        var exception = error.Exception;
        var type = exception.GetType().FullName ?? exception.GetType().Name;

        var report = new ErrorReport(
            ErrorSource.Server,
            Cut(type, ErrorGroup.TypeMaxLength),
            Cut(exception.Message, ErrorGroup.MessageMaxLength),
            Cut(error.Location, ErrorGroup.LocationMaxLength),
            Cut(exception.ToString(), ErrorOccurrence.StackMaxLength),
            Blank(error.RequestId, ErrorOccurrence.RequestIdMaxLength),
            ServerCommit);

        return (report, exception.StackTrace ?? string.Empty);
    }

    /// <summary>
    /// What the browser sent, cut to size. The page is a path and nothing else: a query or a fragment is
    /// dropped, as either may carry what the visitor typed; anything that is not a path is <c>unknown</c>.
    /// </summary>
    public static ErrorReport FromBrowser(string? type, string? message, string? stack, string? page, string? commit, string? requestId) => new(
        ErrorSource.Browser,
        Cut(string.IsNullOrWhiteSpace(type) ? "Error" : type.Trim(), ErrorGroup.TypeMaxLength),
        Cut(message?.Trim() ?? string.Empty, ErrorGroup.MessageMaxLength),
        Path(page),
        Blank(stack, ErrorOccurrence.StackMaxLength),
        Blank(requestId, ErrorOccurrence.RequestIdMaxLength),
        Blank(commit, ErrorOccurrence.CommitMaxLength));

    internal static string Path(string? page)
    {
        if (page is null || !page.StartsWith('/') || page.StartsWith("//", StringComparison.Ordinal))
        {
            return "unknown";
        }

        var end = page.IndexOfAny(['?', '#']);
        var path = end >= 0 ? page[..end] : page;

        return path.Length > ErrorGroup.LocationMaxLength || path.Any(char.IsControl) ? "unknown" : path;
    }

    internal static string? CommitOf(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        return plus >= 0 ? Blank(informationalVersion![(plus + 1)..], ErrorOccurrence.CommitMaxLength) : null;
    }

    private static string Cut(string value, int length) => value.Length <= length ? value : value[..length];

    private static string? Blank(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : Cut(value.Trim(), length);
}
