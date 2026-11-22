namespace Ritocode.Modules.Errors.Domain;

/// <summary>Where an error happened: in the API, or in a visitor's browser.</summary>
public enum ErrorSource
{
    Server,
    Browser,
}

/// <summary>
/// Every occurrence of one error, as its fingerprint says (ADR 0020 decision 1): how often, when first and
/// last, and whether the maintainer has resolved it. A group is kept until it is resolved and stays after;
/// an occurrence of a resolved group reopens it.
/// </summary>
/// <remarks>
/// It holds no request body, no address and no user id: the occurrences' request ids lead to the log
/// lines, which have the rest. The message and location are the latest occurrence's.
/// </remarks>
public sealed class ErrorGroup
{
    public const int FingerprintLength = 64;
    public const int TypeMaxLength = 200;
    public const int MessageMaxLength = 1000;
    public const int LocationMaxLength = 300;

    private ErrorGroup()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>SHA-256, in hex, of the source, the type and the top frames (<see cref="Recording.Fingerprint"/>).</summary>
    public string Fingerprint { get; private set; } = string.Empty;

    public ErrorSource Source { get; private set; }

    /// <summary>The exception's type (<c>System.InvalidOperationException</c>, <c>TypeError</c>).</summary>
    public string Type { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    /// <summary>The method and route template on the server, the page's path in the browser.</summary>
    public string Location { get; private set; } = string.Empty;

    public long Count { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>When the maintainer marked it resolved; <see langword="null"/> while open.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static ErrorGroup Open(string fingerprint, ErrorSource source, string type, string message, string location, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        Fingerprint = fingerprint,
        Source = source,
        Type = type,
        Message = message,
        Location = location,
        Count = 1,
        FirstSeenAt = now,
        LastSeenAt = now,
    };

    public void Resolve(DateTimeOffset now) => ResolvedAt ??= now;
}

/// <summary>
/// One time an error happened. Kept 30 days, and at most the latest 50 per group (ADR 0020); its group
/// keeps the count.
/// </summary>
public sealed class ErrorOccurrence
{
    public const int RequestIdMaxLength = 128;
    public const int StackMaxLength = 8000;
    public const int CommitMaxLength = 64;

    private ErrorOccurrence()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// The request's id: on the server the failed request's, in the browser the report's own — either
    /// finds its line in the API's logs.
    /// </summary>
    public string? RequestId { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public string? Stack { get; private set; }

    /// <summary>The commit the code that failed was built from, when the build was told it.</summary>
    public string? Commit { get; private set; }

    public static ErrorOccurrence Of(Guid groupId, ErrorReport report, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        GroupId = groupId,
        OccurredAt = now,
        RequestId = report.RequestId,
        Message = report.Message,
        Location = report.Location,
        Stack = report.Stack,
        Commit = report.Commit,
    };
}

/// <summary>
/// An error, from either source, already cut to the columns' lengths (<see cref="Recording.ErrorReports"/>).
/// </summary>
public sealed record ErrorReport(
    ErrorSource Source,
    string Type,
    string Message,
    string Location,
    string? Stack,
    string? RequestId,
    string? Commit);
