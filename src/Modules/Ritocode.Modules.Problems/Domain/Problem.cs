namespace Ritocode.Modules.Problems.Domain;

/// <summary>
/// A training task in the catalog. The content a user actually works on lives in a
/// <see cref="ProblemVersion"/>; this row carries only what stays stable across versions.
/// </summary>
public sealed class Problem
{
    public const int SlugMaxLength = 128;
    public const int TitleMaxLength = 200;

    public Guid Id { get; set; }

    /// <summary>
    /// Stable, human-readable identifier used in catalog URLs. Not in docs/DOMAIN_MODEL.md
    /// originally; added so a problem's address survives a title change.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public Difficulty Difficulty { get; set; }

    /// <summary>Markdown shown on the problem detail screen.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Free-form catalog filters, for example <c>refactoring</c>, <c>code-quality</c>.</summary>
    public string[] Tags { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public static Problem Create(
        string slug,
        string title,
        Difficulty difficulty,
        string description,
        string[] tags,
        DateTimeOffset createdAt) => new()
        {
            Id = Guid.CreateVersion7(),
            Slug = slug.Trim().ToLowerInvariant(),
            Title = title,
            Difficulty = difficulty,
            Description = description,
            Tags = tags,
            CreatedAt = createdAt.ToUniversalTime(),
        };
}
