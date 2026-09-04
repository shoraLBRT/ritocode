using System.Text;
using FluentValidation.Results;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// Collects everything wrong with a package and reports it as one validation
/// <see cref="AppError"/>, keyed by manifest path.
/// </summary>
/// <remarks>
/// One error per run would mean an author fixes a package one line at a time, so every check runs
/// and every failure is reported. The field names are manifest paths — <c>validators[1].weight</c>
/// — rather than the C# property names FluentValidation produces, because the author is looking at
/// the YAML, not at this assembly.
/// </remarks>
internal sealed class ManifestErrors
{
    private readonly SortedDictionary<string, List<string>> _byField = new(StringComparer.Ordinal);

    public bool Any => _byField.Count > 0;

    public void Add(string field, string message)
    {
        if (!_byField.TryGetValue(field, out var messages))
        {
            messages = [];
            _byField[field] = messages;
        }

        messages.Add(message);
    }

    public void Add(ValidationResult result)
    {
        foreach (var failure in result.Errors)
        {
            Add(ToManifestPath(failure.PropertyName), failure.ErrorMessage);
        }
    }

    public AppError ToError(string message) => AppError.Validation(
        message,
        _byField.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));

    /// <summary>
    /// <c>Validators[1].TimeoutSeconds</c> becomes <c>validators[1].timeout_seconds</c>, the key
    /// the author actually wrote.
    /// </summary>
    public static string ToManifestPath(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        return string.Join('.', propertyName.Split('.').Select(ToManifestSegment));
    }

    private static string ToManifestSegment(string segment)
    {
        var index = segment.IndexOf('[', StringComparison.Ordinal);
        var name = index < 0 ? segment : segment[..index];
        var subscript = index < 0 ? string.Empty : segment[index..];

        var builder = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                builder.Append(name[i]);
            }
        }

        return builder.Append(subscript).ToString();
    }
}
