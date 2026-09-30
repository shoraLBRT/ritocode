namespace Ritocode.Modules.Problems.ContentFormat;

public enum ContentSeverity
{
    /// <summary>The content cannot be ingested until this is fixed.</summary>
    Error,

    /// <summary>Reported to the author, never blocking.</summary>
    Warning,
}

/// <summary>One thing wrong with the content, at a path relative to the content root.</summary>
public sealed record ContentIssue(ContentSeverity Severity, string Path, string Message)
{
    public override string ToString() =>
        $"{(Severity == ContentSeverity.Error ? "error" : "warning")}: {Path}: {Message}";
}

/// <summary>
/// Everything wrong with a content tree, collected rather than thrown, so one run of validation
/// tells an author all they have to fix (docs/CONTENT_FORMAT.md §7).
/// </summary>
public sealed class ContentReport
{
    private readonly List<ContentIssue> issues = [];

    public IReadOnlyList<ContentIssue> Issues => issues;

    public IEnumerable<ContentIssue> Errors => issues.Where(issue => issue.Severity == ContentSeverity.Error);

    public IEnumerable<ContentIssue> Warnings => issues.Where(issue => issue.Severity == ContentSeverity.Warning);

    public bool HasErrors => issues.Any(issue => issue.Severity == ContentSeverity.Error);

    public void Error(string path, string message) => issues.Add(new ContentIssue(ContentSeverity.Error, path, message));

    public void Warning(string path, string message) => issues.Add(new ContentIssue(ContentSeverity.Warning, path, message));
}
