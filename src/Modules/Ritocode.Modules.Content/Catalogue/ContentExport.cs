using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Ritocode.Modules.Content.Format;

namespace Ritocode.Modules.Content.Catalogue;

/// <summary>
/// What the frontend's build reads to prerender the public pages (docs/SPEC.md §4.1): the problem
/// catalogue exactly as <c>GET /problems</c> serves it, made from the files by the backend's own
/// parser so the format is read in one place.
/// </summary>
public sealed record ContentExport(ProblemCatalogueView Problems)
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

        return new ContentExport(ProblemCatalogueMapping.Map(content.Taxonomy, content.Cards));
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
