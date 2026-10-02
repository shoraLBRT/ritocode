namespace Ritocode.Modules.Content.Format;

/// <summary>
/// A localised text file of a card or a task: YAML front matter between <c>---</c> lines, then
/// Markdown sections under second-level headings. The headings are fixed English keys in every
/// locale, so the parser never depends on the language of the content.
/// </summary>
internal sealed record LocaleDocument(string FrontMatter, IReadOnlyDictionary<string, string> Sections)
{
    /// <summary>The line of the file the front matter starts on: the one after the opening fence.</summary>
    public const int FrontMatterLine = 2;

    private const string Fence = "---";

    /// <summary>The document, or null with every fault reported against <paramref name="path"/>.</summary>
    public static LocaleDocument? Parse(string text, string path, ContentReport report)
    {
        var lines = Normalise(text).Split('\n');

        if (lines.Length == 0 || lines[0] != Fence)
        {
            report.Error(path, "The file must start with front matter between '---' lines.");
            return null;
        }

        var closing = Array.IndexOf(lines, Fence, 1);

        if (closing < 0)
        {
            report.Error(path, "The front matter is never closed with a '---' line.");
            return null;
        }

        var frontMatter = string.Join('\n', lines[1..closing]);
        var sections = SplitSections(lines[(closing + 1)..], "## ", path, report);

        return sections is null ? null : new LocaleDocument(frontMatter, sections);
    }

    /// <summary>
    /// Splits Markdown into sections under headings that start with <paramref name="marker"/>. Text
    /// before the first heading is a fault, as is a heading written twice.
    /// </summary>
    public static Dictionary<string, string>? SplitSections(
        IReadOnlyList<string> lines,
        string marker,
        string path,
        ContentReport report)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        string? heading = null;
        var body = new List<string>();
        var ok = true;

        void Close()
        {
            if (heading is null)
            {
                return;
            }

            if (!sections.TryAdd(heading, string.Join('\n', body).Trim()))
            {
                report.Error(path, $"The heading '{marker}{heading}' appears twice.");
                ok = false;
            }
        }

        foreach (var line in lines)
        {
            if (line.StartsWith(marker, StringComparison.Ordinal))
            {
                Close();
                heading = line[marker.Length..].Trim();
                body.Clear();
                continue;
            }

            if (heading is null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    report.Error(path, $"Text outside any '{marker.Trim()}' section: '{line.Trim()}'.");
                    ok = false;
                }

                continue;
            }

            body.Add(line);
        }

        Close();

        return ok ? sections : null;
    }

    public static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
