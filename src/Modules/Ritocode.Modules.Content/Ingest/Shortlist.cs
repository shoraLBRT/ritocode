using System.Security.Cryptography;
using System.Text;

namespace Ritocode.Modules.Content.Ingest;

/// <summary>
/// The cards an easy task offers in step 1 (docs/SPEC.md §4.4): its own findings plus up to
/// <see cref="Extras"/> others from the whole catalogue. Chosen deterministically from the task's
/// slug, so every learner — and every ingest of the same content — sees the same list.
/// </summary>
public static class Shortlist
{
    public const int Extras = 20;

    public static IReadOnlyList<string> For(string taskSlug, IEnumerable<string> findings, IEnumerable<string> catalogue)
    {
        ArgumentNullException.ThrowIfNull(taskSlug);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(catalogue);

        var own = findings.ToHashSet(StringComparer.Ordinal);

        var extras = catalogue
            .Where(card => !own.Contains(card))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(card => Rank(taskSlug, card))
            .ThenBy(card => card, StringComparer.Ordinal)
            .Take(Extras);

        return [.. own.Concat(extras).Order(StringComparer.Ordinal)];
    }

    // A hash rather than string.GetHashCode, which differs between processes.
    private static ulong Rank(string taskSlug, string card) =>
        BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"{taskSlug}\n{card}")), 0);
}
