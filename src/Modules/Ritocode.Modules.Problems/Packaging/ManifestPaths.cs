using System.Text.RegularExpressions;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// The two patterns and the one path rule the manifest format is built on, in one place so the
/// validator and the loader cannot disagree about them.
/// </summary>
internal static partial class ManifestPaths
{
    /// <summary>Lower-case kebab: slugs, tags, validator ids and validator types.</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex Kebab { get; }

    /// <summary>A language selector, for example <c>csharp</c>, <c>c++</c>, <c>f#</c>.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9+#.-]*$")]
    public static partial Regex Language { get; }

    /// <summary>
    /// A path inside the package: relative, forward-slashed, and unable to leave the package.
    /// Set <paramref name="allowWildcards"/> for the glob lists, where <c>*</c> and <c>?</c> are
    /// part of the syntax rather than an odd file name.
    /// </summary>
    public static bool IsInsidePackage(string? path, bool allowWildcards = false)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.StartsWith('/')
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        if (!allowWildcards && path.AsSpan().IndexOfAny('*', '?') >= 0)
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            // An empty segment is a doubled slash or a trailing one; "." and ".." are the two ways
            // a relative path stops being confined to where it started.
            if (segment.Length == 0 || segment is "." or ".." || segment.Trim() != segment)
            {
                return false;
            }
        }

        return true;
    }
}
