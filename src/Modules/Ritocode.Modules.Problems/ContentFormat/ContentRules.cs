using System.Text.RegularExpressions;

namespace Ritocode.Modules.Problems.ContentFormat;

/// <summary>The fixed values of docs/CONTENT_FORMAT.md and docs/SPEC.md §3, in one place.</summary>
public static partial class ContentRules
{
    /// <summary>Every card, task and taxonomy entry must have text in this locale.</summary>
    public const string DefaultLocale = "ru";

    public const int SlugMaxLength = 64;

    public const int MinWeight = 1;

    public const int MaxWeight = 3;

    /// <summary>Lines longer than this scroll sideways on a phone (SPEC §3.4).</summary>
    public const int MaxLineLength = 79;

    /// <summary>The fewest cards an easy task's shortlist adds beside its own findings (SPEC §4.4).</summary>
    public const int MinShortlistExtras = 15;

    /// <summary>Material languages the MVP accepts (SPEC §3.4).</summary>
    public static IReadOnlySet<string> Languages { get; } = new HashSet<string>(StringComparer.Ordinal) { "python" };

    /// <summary>The size band of each difficulty (SPEC §3.4): total lines and files, at most.</summary>
    public static (int Lines, int Files) SizeBand(TaskDifficulty difficulty) => difficulty switch
    {
        TaskDifficulty.Easy => (80, 2),
        TaskDifficulty.Medium => (300, 6),
        TaskDifficulty.Hard => (600, 12),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };

    public static bool IsSlug(string value) => value.Length <= SlugMaxLength && SlugPattern().IsMatch(value);

    public static bool IsLocale(string value) => LocalePattern().IsMatch(value);

    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalePattern();
}
