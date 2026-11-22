using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Ritocode.Modules.Content.Format;
using Ritocode.Modules.Content.Ingest;

namespace Ritocode.Modules.Content.Catalogue;

/// <summary>
/// What the frontend's build reads to prerender the public pages (docs/SPEC.md §4.1): the problem
/// catalogue exactly as <c>GET /problems</c> serves it, made from the files by the backend's own
/// parser so the format is read in one place — and the tasks, in the task catalogue's order, each
/// with only what its public page shows (#301). The build writes a page for each, so the web image
/// answers 404 for a task address that names no task (#300).
/// </summary>
public sealed record ContentExport(ProblemCatalogueView Problems, IReadOnlyList<ExportedTask> Tasks)
{
    private static readonly string[] DifficultyOrder = ["easy", "medium", "hard"];

    /// <summary>The API's shape — camelCase, nulls written — with text left readable.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    public static ContentExport From(ContentSet content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The order of GET /tasks: difficulty, then title.
        var tasks = content.Tasks
            .Select(task => (Task: task, Text: task.Texts.GetValueOrDefault(ContentRules.DefaultLocale)))
            .Where(pair => pair.Text is not null)
            .Select(pair => new ExportedTask(
                pair.Task.Slug,
                pair.Text!.Title,
                ContentIngest.DifficultyName(pair.Task.Difficulty),
                pair.Text.Context,
                pair.Text.Brief))
            .OrderBy(task => Array.IndexOf(DifficultyOrder, task.Difficulty))
            .ThenBy(task => task.Title, StringComparer.Ordinal)
            .ToList();

        return new ContentExport(ProblemCatalogueMapping.Map(content.Taxonomy, content.Cards), tasks);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

/// <summary>
/// A task as its public page shows it, in the default locale: the title, the difficulty, the context
/// and the brief — what the task screen shows before the learner answers. Never the answer key, the
/// notes, the lesson, a card weight or the cards to pick from: a test holds the export to these five.
/// </summary>
public sealed record ExportedTask(string Slug, string Title, string Difficulty, string Context, string Brief);
