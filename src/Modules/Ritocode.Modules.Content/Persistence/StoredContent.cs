namespace Ritocode.Modules.Content.Persistence;

// The content schema's rows. Everything localised, and every list an item carries, is stored as
// JSON beside the columns rather than in tables of its own: content is read whole, by slug, and
// never queried by a field inside a card or a task. See docs/DATABASE_SCHEMA.md.

/// <summary>The one taxonomy: classes and the treatment tree with their labels. Always id 1.</summary>
public sealed class StoredTaxonomy
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>The <see cref="Format.Taxonomy"/>, as JSON.</summary>
    public string Document { get; set; } = "{}";

    public string ContentRevision { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StoredCard
{
    public string Slug { get; set; } = string.Empty;

    public string Class { get; set; } = string.Empty;

    public int Weight { get; set; }

    /// <summary>The card's text per locale, as JSON.</summary>
    public string Texts { get; set; } = "{}";

    /// <summary>Set when the card left <c>content/</c>. A retired card is never deleted: attempts name it.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    public string ContentRevision { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StoredMaterial
{
    public string Slug { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    /// <summary>Every file with its content, as JSON.</summary>
    public string Files { get; set; } = "[]";

    /// <summary>The <see cref="Ingest.MaterialOverview"/>, derived at ingest, as JSON.</summary>
    public string Overview { get; set; } = "{}";

    public string ContentRevision { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StoredTask
{
    public string Slug { get; set; } = string.Empty;

    public string Material { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    /// <summary>The answer key, as JSON. Never leaves the server except inside a submitted attempt.</summary>
    public string Findings { get; set; } = "[]";

    /// <summary>The task's text per locale, as JSON.</summary>
    public string Texts { get; set; } = "{}";

    /// <summary>The cards an easy task offers in step 1; empty for other difficulties.</summary>
    public string[] Shortlist { get; set; } = [];

    /// <summary>Set when the task left <c>content/</c>. An unpublished task is never deleted: attempts name it.</summary>
    public DateTimeOffset? UnpublishedAt { get; set; }

    public string ContentRevision { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
