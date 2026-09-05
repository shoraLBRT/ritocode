using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Ritocode.Modules.Problems.Packaging.Yaml;

/// <summary>
/// Reads a validator's free-form <c>with:</c> mapping into a <see cref="JsonObject"/>, so it can be
/// stored in <c>problem_versions.validator_config</c> without the manifest having to know what any
/// plugin's configuration looks like.
/// </summary>
/// <remarks>
/// Two properties make the result usable as stored state. Plain scalars are resolved with the YAML
/// core schema — <c>120</c> is a number, <c>"120"</c> a string, <c>true</c> a boolean — so a plugin
/// reading JSON sees the types its author wrote. And mapping keys are inserted in ordinal order,
/// which is what makes the projection in <see cref="ValidatorPipeline"/> canonical: the same
/// pipeline serialises byte-identically however the YAML was ordered.
/// </remarks>
internal sealed class JsonObjectYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(JsonObject);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        ArgumentNullException.ThrowIfNull(parser);

        var node = ReadNode(parser);

        return node switch
        {
            JsonObject mapping => mapping,
            null => null,
            _ => throw new YamlException("Expected a mapping."),
        };
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
        // Manifests are authored by hand and read by the loader; nothing writes one back out.
        throw new NotSupportedException("Problem manifests are read-only.");

    private static JsonNode? ReadNode(IParser parser)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return ReadScalar(scalar);
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            var items = new JsonArray();

            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                items.Add(ReadNode(parser));
            }

            return items;
        }

        if (parser.TryConsume<MappingStart>(out _))
        {
            // Collected first, then inserted in key order: JsonObject preserves insertion order,
            // and sorting here is what removes authoring order from the stored JSON.
            var entries = new SortedDictionary<string, JsonNode?>(StringComparer.Ordinal);

            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>();

                if (!entries.TryAdd(key.Value, ReadNode(parser)))
                {
                    throw new YamlException(key.Start, key.End, $"Duplicate key '{key.Value}'.");
                }
            }

            var mapping = new JsonObject();

            foreach (var (key, value) in entries)
            {
                mapping.Add(key, value);
            }

            return mapping;
        }

        var current = parser.Current;
        throw new YamlException(
            current?.Start ?? Mark.Empty,
            current?.End ?? Mark.Empty,
            "Anchors, aliases and tags are not supported in a problem manifest.");
    }

    private static JsonValue? ReadScalar(Scalar scalar)
    {
        // A quoted scalar is a string by definition; only plain scalars carry an inferred type.
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(scalar.Value);
        }

        var value = scalar.Value;

        if (value.Length == 0 || value is "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        if (value is "true" or "True" or "TRUE")
        {
            return JsonValue.Create(true);
        }

        if (value is "false" or "False" or "FALSE")
        {
            return JsonValue.Create(false);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
        {
            return JsonValue.Create(number);
        }

        return JsonValue.Create(value);
    }
}
