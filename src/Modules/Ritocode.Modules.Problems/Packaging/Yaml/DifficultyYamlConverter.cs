using Ritocode.Modules.Problems.Domain;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Ritocode.Modules.Problems.Packaging.Yaml;

/// <summary>
/// Reads <c>difficulty</c> as one of exactly <c>easy</c>, <c>medium</c>, <c>hard</c>.
/// </summary>
/// <remarks>
/// YamlDotNet's own enum handling is case-insensitive and accepts the underlying number, so
/// <c>difficulty: 1</c> would quietly mean <c>medium</c>. A manifest is content under review; it
/// should have one spelling per value, and say so when it does not.
/// </remarks>
internal sealed class DifficultyYamlConverter : IYamlTypeConverter
{
    private static readonly Dictionary<string, Difficulty> Values = new(StringComparer.Ordinal)
    {
        ["easy"] = Difficulty.Easy,
        ["medium"] = Difficulty.Medium,
        ["hard"] = Difficulty.Hard,
    };

    public bool Accepts(Type type) => type == typeof(Difficulty);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        ArgumentNullException.ThrowIfNull(parser);

        var scalar = parser.Consume<Scalar>();

        return Values.TryGetValue(scalar.Value, out var difficulty)
            ? difficulty
            : throw new YamlException(
                scalar.Start,
                scalar.End,
                $"'{scalar.Value}' is not a difficulty; expected {string.Join(", ", Values.Keys)}.");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
        throw new NotSupportedException("Problem manifests are read-only.");
}
