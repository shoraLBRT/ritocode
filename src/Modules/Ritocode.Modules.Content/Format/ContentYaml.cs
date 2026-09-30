using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ritocode.Modules.Content.Format;

/// <summary>
/// Reads the YAML of a content file into one of the shapes below. Strict on purpose: a key the
/// format does not know is a typo, and a key written twice is an edit that lost half of itself.
/// </summary>
internal static class ContentYaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .Build();

    /// <summary>The document, or null with the reason reported against <paramref name="path"/>.</summary>
    public static T? Read<T>(string text, string path, ContentReport report)
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
        catch (YamlException ex)
        {
            var where = ex.Start.Line > 0 ? $"line {ex.Start.Line}, column {ex.Start.Column}: " : string.Empty;
            report.Error(path, $"{where}{Innermost(ex).Message}");
            return null;
        }
    }

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
