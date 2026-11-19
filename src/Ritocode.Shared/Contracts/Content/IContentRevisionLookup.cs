namespace Ritocode.Shared.Contracts.Content;

/// <summary>
/// Answers which revision of <c>content/</c> was ingested last, and when — what the admin area's system page
/// shows (#297).
/// </summary>
/// <remarks>A cross-module contract in the sense of ADR 0007, implemented by the Content module.</remarks>
public interface IContentRevisionLookup
{
    /// <summary>The last ingest, or <see langword="null"/> when content has never been ingested.</summary>
    Task<ContentRevision?> LastAsync(CancellationToken cancellationToken);
}

/// <param name="Revision">The commit the content came from, as the ingest was stamped with it.</param>
/// <param name="IngestedAt">When that ingest was written.</param>
public sealed record ContentRevision(string Revision, DateTimeOffset IngestedAt);
