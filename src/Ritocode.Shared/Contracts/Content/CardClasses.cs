namespace Ritocode.Shared.Contracts.Content;

/// <summary>What <see cref="ICardClassLookup"/> reports (ADR 0007 §3). Names are in the default locale.</summary>
/// <param name="Classes">Every class, in the taxonomy's order.</param>
/// <param name="ClassOf">The class of each card asked about that exists; an unknown slug is absent.</param>
/// <param name="ClassNames">The name of every class; a class without text is named by its id.</param>
/// <param name="CardNames">The name of each card asked about that exists, retired ones included.</param>
public sealed record CardClasses(
    IReadOnlyList<string> Classes,
    IReadOnlyDictionary<string, string> ClassOf,
    IReadOnlyDictionary<string, string> ClassNames,
    IReadOnlyDictionary<string, string> CardNames);
