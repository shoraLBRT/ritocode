namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// The reference package from <c>content/problems</c>, copied into the test output by the project
/// file so the committed content is what gets checked.
/// </summary>
internal static class ExamplePackage
{
    public const string Slug = "example-order-total";

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "content", "problems", Slug);
}
