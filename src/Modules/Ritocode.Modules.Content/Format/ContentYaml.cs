using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ritocode.Modules.Content.Format;

/// <summary>
/// Reads the YAML of a content file into one of the shapes below. Strict on purpose: a key the
/// format does not know is a typo, and a key written twice is an edit that lost half of itself.
/// </summary>
internal static class ContentYaml
{
    private const string QuoteHint = "Put a value that contains ': ' in quotes";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .WithNodeDeserializer(new TextNodeDeserializer(), where => where.OnTop())
        .Build();

    /// <summary>The document, or null with the reason reported against <paramref name="path"/>.</summary>
    /// <param name="firstLine">The line of the file that <paramref name="text"/> starts on, so a reported line is the file's.</param>
    public static T? Read<T>(string text, string path, ContentReport report, int firstLine = 1)
        where T : class
    {
        try
        {
            var value = Deserializer.Deserialize<T?>(text);

            if (value is null)
            {
                report.Error(path, "The document is empty.");
            }

            return value;
        }
        catch (NotTextException ex)
        {
            report.Error(path, $"{Where(ex.Start, firstLine)}{FieldAt(text, ex.Start)}{ex.Message}");
            return null;
        }
        catch (SemanticErrorException ex)
        {
            report.Error(path, $"{Where(ex.Start, firstLine)}{ex.Message}{ColonHint(text, ex.Start)}");
            return null;
        }
        catch (YamlException ex)
        {
            report.Error(path, $"{Where(ex.Start, firstLine)}{Innermost(ex).Message}");
            return null;
        }
    }

    private static string Where(Mark mark, int firstLine) =>
        mark.Line > 0 ? $"line {mark.Line + firstLine - 1}, column {mark.Column}: " : string.Empty;

    // YamlDotNet wraps the real cause — an unknown key, a wrong type — in a generic message.
    private static Exception Innermost(Exception exception)
    {
        var current = exception;

        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current;
    }

    // "'keywords': " — the top-level field the mark falls under, so the author knows which one to
    // fix; the item itself is named by its text. YamlDotNet keeps no end position for a list or a
    // mapping, so the field is the last key written before the mark. Empty when there is none.
    private static string FieldAt(string text, Mark mark)
    {
        var stream = new YamlStream();

        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException)
        {
            return string.Empty;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return string.Empty;
        }

        var field = root.Children.Keys
            .OfType<YamlScalarNode>()
            .LastOrDefault(key => key.Start.Index <= mark.Index);

        return field is null ? string.Empty : $"'{field.Value}': ";
    }

    // A plain value with a second ': ' in it — `name: Ошибка: подробности` — fails in the scanner,
    // whose message says nothing about quoting.
    private static string ColonHint(string text, Mark mark)
    {
        var lines = LocaleDocument.Normalise(text).Split('\n');

        if (mark.Line < 1 || mark.Line > lines.Length)
        {
            return string.Empty;
        }

        var line = lines[(int)mark.Line - 1];
        var colon = line.IndexOf(": ", StringComparison.Ordinal);

        if (colon < 0 || line.IndexOf(": ", colon + 2, StringComparison.Ordinal) < 0)
        {
            return string.Empty;
        }

        return $" {QuoteHint}, as in {line[..(colon + 2)].Trim()} \"{line[(colon + 2)..].Trim()}\".";
    }

    /// <summary>A mapping or a list where the format wants text — almost always an unquoted ': '.</summary>
    private sealed class NotTextException(Mark start, Mark end, string message) : YamlException(start, end, message);

    /// <summary>
    /// Stands in front of YamlDotNet's own deserializers for <see cref="string"/>. Left to them, a
    /// mapping in a text field — <c>keywords: [noqa, type: ignore]</c> — fails with "Cannot
    /// dynamically create an instance of type 'System.String'", naming neither the field nor the cause.
    /// </summary>
    private sealed class TextNodeDeserializer : INodeDeserializer
    {
        public bool Deserialize(
            IParser reader,
            Type expectedType,
            Func<IParser, Type, object?> nestedObjectDeserializer,
            out object? value,
            ObjectDeserializer rootDeserializer)
        {
            value = null;

            var node = reader.Current;

            if (expectedType != typeof(string) || node is not (MappingStart or SequenceStart))
            {
                return false;
            }

            var scalars = new List<Mark>();
            var written = Render(reader, nested: false, scalars);

            // A key-value pair inside a flow list has no position of its own; its first scalar has.
            var start = scalars.Count > 0 ? scalars[0] : node.Start;

            throw new NotTextException(
                start,
                node.End,
                node is MappingStart
                    ? $"YAML reads '{written}' as a key and a value, not as text. {QuoteHint}, as in \"{written}\"."
                    : $"YAML reads '{written}' as a list, not as text. Put the value in quotes, as in \"{written}\".");
        }

        // Consumes the node and writes it back roughly as it was typed.
        private static string Render(IParser reader, bool nested, List<Mark> scalars)
        {
            if (reader.TryConsume<Scalar>(out var scalar))
            {
                scalars.Add(scalar.Start);
                return scalar.Value;
            }

            if (reader.TryConsume<MappingStart>(out _))
            {
                var pairs = new List<string>();

                while (!reader.TryConsume<MappingEnd>(out _))
                {
                    var key = Render(reader, nested: true, scalars);
                    pairs.Add($"{key}: {Render(reader, nested: true, scalars)}");
                }

                var joined = string.Join(", ", pairs);
                return nested ? $"{{{joined}}}" : joined;
            }

            if (reader.TryConsume<SequenceStart>(out _))
            {
                var items = new List<string>();

                while (!reader.TryConsume<SequenceEnd>(out _))
                {
                    items.Add(Render(reader, nested: true, scalars));
                }

                return $"[{string.Join(", ", items)}]";
            }

            reader.SkipThisAndNestedEvents();
            return "…";
        }
    }
}

public sealed record ClassesDocument
{
    public string[]? Classes { get; init; }
}

public sealed record TreatmentsDocument
{
    public BranchDocument[]? Branches { get; init; }
}

public sealed record BranchDocument
{
    public string? Id { get; init; }

    public string[]? Leaves { get; init; }
}

public sealed record TaxonomyLocaleDocument
{
    public Dictionary<string, ClassLabelDocument>? Classes { get; init; }

    public Dictionary<string, BranchLabelDocument>? Treatments { get; init; }
}

public sealed record ClassLabelDocument
{
    public string? Name { get; init; }

    public string? Description { get; init; }
}

public sealed record BranchLabelDocument
{
    public string? Name { get; init; }

    public Dictionary<string, string>? Leaves { get; init; }
}

public sealed record CardDocument
{
    public string? Class { get; init; }

    public int? Weight { get; init; }
}

public sealed record CardFrontMatter
{
    public string? Name { get; init; }

    public string? Summary { get; init; }

    public string[]? Keywords { get; init; }
}

public sealed record MaterialDocument
{
    public string? Language { get; init; }

    public string? Notes { get; init; }
}

public sealed record TaskDocument
{
    public string? Material { get; init; }

    public string? Difficulty { get; init; }

    public FindingDocument[]? Findings { get; init; }
}

public sealed record FindingDocument
{
    public string? Card { get; init; }

    public string[]? Leaves { get; init; }
}

public sealed record TaskFrontMatter
{
    public string? Title { get; init; }
}
