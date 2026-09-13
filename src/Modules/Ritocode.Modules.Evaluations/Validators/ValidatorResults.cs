using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ritocode.Modules.Evaluations.Validators;

/// <summary>
/// The canonical JSON of <c>submission_reports.validator_results</c>: the result schema of #18, and
/// what the determinism test of #38 compares.
/// </summary>
/// <remarks>
/// <para>
/// Canonical in the sense <c>validator_config</c> is (docs/PROBLEM_PACKAGE_SPEC.md): the same results
/// produce byte-identical JSON however they were assembled. Every field is written, <c>null</c>
/// included; field order is fixed here; steps keep pipeline order, which is meaningful; checks are
/// sorted ordinally, which is not; and enums are camelCase names, never ordinals.
/// </para>
/// <para>
/// Nothing in it varies between two runs of one submission — no duration, no timestamp, no raw output —
/// so two evaluations of the same input produce the same bytes. A digest of it is computed from this
/// string before the write, never read back from the <c>jsonb</c> column, which reorders keys.
/// </para>
/// </remarks>
public static class ValidatorResults
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static string ToJson(IReadOnlyList<ValidatorResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var validators = new JsonArray();

        foreach (var result in results)
        {
            var checks = new JsonArray();

            // Sorted here as well as by the factory, so the rule holds for anything this is handed.
            foreach (var check in result.Checks.OrderBy(check => check.Name, StringComparer.Ordinal))
            {
                checks.Add(new JsonObject
                {
                    ["name"] = check.Name,
                    ["outcome"] = Name(check.Outcome),
                });
            }

            validators.Add(new JsonObject
            {
                ["id"] = result.Id,
                ["type"] = result.Type,
                ["outcome"] = Name(result.Outcome),
                ["runOutcome"] = result.RunOutcome is { } runOutcome ? Name(runOutcome) : null,
                ["summary"] = result.Summary,
                ["checks"] = checks,
            });
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["validators"] = validators,
        };

        return document.ToJsonString(SerializerOptions);
    }

    private static string Name<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
