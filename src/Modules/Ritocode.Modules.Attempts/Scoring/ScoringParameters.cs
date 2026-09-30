using System.ComponentModel.DataAnnotations;

namespace Ritocode.Modules.Attempts.Scoring;

/// <summary>
/// The numbers of docs/SPEC.md §5.2, in configuration so they can be tuned once real attempts exist.
/// Every value is a magnitude: a reward is added, a penalty subtracted. Tuning them never rewrites a
/// stored result (SPEC §5.4).
/// </summary>
public sealed class ScoringParameters
{
    public const string SectionName = "Attempts:Scoring";

    /// <summary>A card in the key is picked: + this × weight.</summary>
    [Range(0, 1000)]
    public int Found { get; init; } = 10;

    /// <summary>A card in the key is not picked: − this × weight.</summary>
    [Range(0, 1000)]
    public int Missed { get; init; } = 3;

    /// <summary>A card not in the key is picked: − this, flat.</summary>
    [Range(0, 1000)]
    public int Extra { get; init; } = 3;

    /// <summary>For a found card, at least one picked leaf is among the key's: + this × weight.</summary>
    [Range(0, 1000)]
    public int TreatmentMatched { get; init; } = 5;

    /// <summary>For a found card, each picked leaf that is not among the key's: − this.</summary>
    [Range(0, 1000)]
    public int WrongLeaf { get; init; } = 2;

    public static ScoringParameters Default { get; } = new();
}
