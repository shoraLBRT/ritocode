using Ritocode.Modules.Problems.Packaging.Yaml;
using Ritocode.Shared.Errors;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// Turns the text of a <c>problem.yaml</c> into a <see cref="ProblemManifest"/>. Shape only —
/// whether the manifest makes sense is <see cref="ProblemManifestValidator"/>'s question.
/// </summary>
public static class ProblemManifestParser
{
    /// <summary>Field name used for failures that belong to the document rather than to a key.</summary>
    public const string DocumentField = "problem.yaml";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new DifficultyYamlConverter())
        .WithTypeConverter(new JsonObjectYamlConverter())
        // Both are strictness on purpose: a key the loader does not know is a typo, and a key
        // written twice is an edit that silently lost half of itself.
        .WithDuplicateKeyChecking()
        .Build();

    public static Result<ProblemManifest> Parse(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        try
        {
            var manifest = Deserializer.Deserialize<ProblemManifest?>(yaml);

            return manifest is null
                ? Failure("The manifest is empty.")
                : manifest;
        }
        catch (YamlException ex)
        {
            var where = ex.Start.Line > 0
                ? $"line {ex.Start.Line}, column {ex.Start.Column}: "
                : string.Empty;

            return Failure($"{where}{Innermost(ex).Message}");
        }
    }

    /// <summary>
    /// YamlDotNet wraps the real cause — an unknown property, a bad difficulty — in a generic
    /// "exception during deserialization". The inner message is the one an author can act on.
    /// </summary>
    private static Exception Innermost(Exception exception)
    {
        var current = exception;

        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current;
    }

    private static AppError Failure(string message) => AppError.Validation(
        "The problem manifest could not be read.",
        new Dictionary<string, string[]> { [DocumentField] = [message] });
}
