namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// The committed <c>content/problems</c> tree, copied into the test output by the project file so
/// the tests check the packages as they ship rather than a copy someone remembered to update.
/// </summary>
internal static class ContentPackages
{
    /// <summary>
    /// The problems the catalog ships, authored for #42. The reference package is deliberately not
    /// one of them: it exists to keep the format honest, not to be solved.
    /// </summary>
    public static readonly string[] CatalogSlugs =
    [
        "no-double-booking",
        "respect-the-precedence",
        "split-the-invoice",
    ];

    public static string Root => Path.Combine(AppContext.BaseDirectory, "content", "problems");

    public static string DirectoryFor(string slug) => Path.Combine(Root, slug);

    /// <summary>Every package directory in the tree, catalog content and reference alike.</summary>
    public static IReadOnlyList<string> AllSlugs =>
    [
        .. new DirectoryInfo(Root)
            .EnumerateDirectories()
            .Select(directory => directory.Name)
            .Order(StringComparer.Ordinal)
    ];
}
