using Ritocode.Modules.Problems.Packaging;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Problems.Tests;

/// <summary>
/// A throwaway package on disk, so a loader test can state the one thing that is wrong with it and
/// nothing else. Deleted when the test finishes.
/// </summary>
internal sealed class TempPackage : IDisposable
{
    /// <summary>
    /// A manifest that loads. Tests derive from it with <see cref="string.Replace(string, string, StringComparison)"/>
    /// so the diff between a passing package and a failing one is the assertion.
    /// </summary>
    public const string ValidManifest = """
        schema_version: 1
        slug: temp-problem
        title: A temporary problem
        difficulty: easy
        language: csharp
        tags:
          - refactoring
        description: description.md
        workspace:
          root: starter
          editable:
            - src/**/*.cs
          readonly:
            - tests/**
        validators:
          - id: tests
            type: test
            weight: 100
            timeout_seconds: 60
        """;

    /// <summary>
    /// <see cref="ValidManifest"/> with a second validator appended, the two sharing the weight.
    /// Appended rather than spliced: the validator list is the last thing in the manifest.
    /// </summary>
    public static string ManifestWithTwoValidators(string id, string type = "compile") =>
        ValidManifest.Replace("    weight: 100", "    weight: 50", StringComparison.Ordinal)
        + $"\n  - id: {id}\n    type: {type}\n    weight: 50\n    timeout_seconds: 60";

    private TempPackage(string directory) => Directory = directory;

    public string Directory { get; }

    /// <summary>A package with a manifest, a description, one editable file and one read-only file.</summary>
    public static TempPackage Valid() => Empty()
        .With("problem.yaml", ValidManifest)
        .With("description.md", "# A temporary problem\n\nChange the code in `src/`.\n")
        .With("starter/src/Code.cs", "public static class Code { }\n")
        .With("starter/tests/CodeTests.cs", "public sealed class CodeTests { }\n");

    public static TempPackage Empty()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ritocode-package-" + Guid.CreateVersion7().ToString("n"));
        System.IO.Directory.CreateDirectory(directory);

        return new TempPackage(directory);
    }

    public TempPackage With(string relativePath, string content)
    {
        var path = Path.Combine(Directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return this;
    }

    public TempPackage Without(string relativePath)
    {
        File.Delete(Path.Combine(Directory, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        return this;
    }

    /// <summary>Rewrites the manifest, replacing <paramref name="original"/> with <paramref name="replacement"/>.</summary>
    public TempPackage WithManifestChange(string original, string replacement)
    {
        var manifest = File.ReadAllText(Path.Combine(Directory, "problem.yaml"));

        Assert.Contains(original, manifest, StringComparison.Ordinal);

        return With("problem.yaml", manifest.Replace(original, replacement, StringComparison.Ordinal));
    }

    public Result<ProblemPackage> Load() => ProblemPackageLoader.Load(Directory);

    /// <summary>Loads, expecting failure, and returns the fields the loader complained about.</summary>
    public IReadOnlyDictionary<string, string[]> LoadExpectingFailure()
    {
        var result = Load();

        Assert.False(result.IsSuccess, "The package was expected to be invalid.");
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.Error.Fields);

        return result.Error.Fields;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
