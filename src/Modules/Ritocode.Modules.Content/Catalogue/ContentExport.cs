using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Ritocode.Modules.Content.Format;

namespace Ritocode.Modules.Content.Catalogue;

/// <summary>
/// What the frontend's build reads to prerender the public pages (docs/SPEC.md §4.1): the problem
/// catalogue exactly as <c>GET /problems</c> serves it, made from the files by the backend's own
/// parser so the format is read in one place — and the slugs of the tasks, in order, so the web image
/// answers 404 for a task address that names no task (#300).
/// </summary>
public sealed record ContentExport(ProblemCatalogueView Problems, IReadOnlyList<string> Tasks)
{
    /// <summary>The API's shape — camelCase, nulls written — with text left readable.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    public static ContentExport From(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new ContentExport(
            ProblemCatalogueMapping.Map(content.Taxonomy, content.Cards),
            [.. content.Tasks.Select(task => task.Slug).Order(StringComparer.Ordinal)]);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
