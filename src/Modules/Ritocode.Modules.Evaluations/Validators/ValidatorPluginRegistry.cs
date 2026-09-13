using System.Text.RegularExpressions;

namespace Ritocode.Modules.Evaluations.Validators;

/// <summary>The validator plugins this host can run, by the pipeline <c>type</c> each answers.</summary>
public interface IValidatorPluginRegistry
{
    /// <summary>Every registered type, ordered ordinally.</summary>
    IReadOnlyList<string> Types { get; }

    /// <summary>The plugin for <paramref name="type"/>, compared ordinally, or <see langword="null"/> when none is registered.</summary>
    IValidatorPlugin? Find(string type);
}

/// <summary>The registry over every <see cref="IValidatorPlugin"/> the container holds. See <see cref="IValidatorPluginRegistry"/>.</summary>
/// <remarks>
/// A plugin is an ordinary registration, not something discovered by scanning assemblies — ADR 0002
/// rejected scanning because it hides what the composed system does, and a validator nobody can find in a
/// module's <c>RegisterServices</c> is exactly that. The rules that would otherwise surface as a pipeline
/// that silently runs the wrong plugin are checked when the registry is built: a type no manifest could
/// name, and two plugins answering one type.
/// </remarks>
internal sealed partial class ValidatorPluginRegistry : IValidatorPluginRegistry
{
    /// <summary>The manifest's own rule for <c>type</c> (docs/PROBLEM_PACKAGE_SPEC.md).</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TypePattern { get; }

    private const int MaxTypeLength = 32;

    private readonly Dictionary<string, IValidatorPlugin> _plugins = new(StringComparer.Ordinal);

    public ValidatorPluginRegistry(IEnumerable<IValidatorPlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        foreach (var plugin in plugins)
        {
            var type = plugin.Type;

            if (type is not { Length: > 0 and <= MaxTypeLength } || !TypePattern.IsMatch(type))
            {
                throw new InvalidOperationException(
                    $"{plugin.GetType().Name} answers the validator type '{type}', which no manifest can name.");
            }

            if (!_plugins.TryAdd(type, plugin))
            {
                throw new InvalidOperationException(
                    $"The validator type '{type}' is answered by both {_plugins[type].GetType().Name} and {plugin.GetType().Name}.");
            }
        }

        Types = [.. _plugins.Keys.Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<string> Types { get; }

    public IValidatorPlugin? Find(string type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return _plugins.GetValueOrDefault(type);
    }
}
