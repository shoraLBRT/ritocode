using Microsoft.Extensions.FileSystemGlobbing;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Problems.Packaging;

/// <summary>
/// Reads a problem package from disk and checks it against docs/PROBLEM_PACKAGE_SPEC.md.
/// </summary>
/// <remarks>
/// Loading is inspection only. Nothing from a package is ever executed here: running a package's
/// code is the sandbox runner's job and only its job, and a content pipeline is exactly where that
/// rule gets bent by accident — see `docs/AGENT_GUIDELINES.md` and ADR 0005.
/// </remarks>
public static class ProblemPackageLoader
{
    public const string ManifestFileName = "problem.yaml";

    /// <summary>Field name used for failures that belong to the package rather than to a manifest key.</summary>
    public const string PackageField = "package";

    private static readonly ProblemManifestValidator ManifestValidator = new();

    /// <summary>
    /// Loads and checks the package in <paramref name="packageDirectory"/>.
    /// </summary>
    /// <returns>
    /// The package, or a validation <see cref="AppError"/> whose fields are manifest paths.
    /// Parsing and manifest validation gate the file checks: a manifest that does not parse cannot
    /// be compared against files. Past that point every check runs, so one load reports everything
    /// an author has to fix.
    /// </returns>
    public static Result<ProblemPackage> Load(string packageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);

        var root = Path.GetFullPath(packageDirectory);
        var errors = new ManifestErrors();

        if (!Directory.Exists(root))
        {
            errors.Add(PackageField, $"No package directory at '{packageDirectory}'.");
            return errors.ToError(FailureMessage);
        }

        var manifestPath = Path.Combine(root, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            errors.Add(PackageField, $"A package needs a {ManifestFileName} at its root.");
            return errors.ToError(FailureMessage);
        }

        var parsed = ProblemManifestParser.Parse(File.ReadAllText(manifestPath));

        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        var manifest = parsed.Value;
        var validation = ManifestValidator.Validate(manifest);

        if (!validation.IsValid)
        {
            errors.Add(validation);
            return errors.ToError(FailureMessage);
        }

        var description = ReadDescription(root, manifest, errors);
        var workspace = ClassifyWorkspace(root, manifest, errors);
        CheckFixtures(root, manifest, workspace.EditableMatcher, errors);

        if (errors.Any)
        {
            return errors.ToError(FailureMessage);
        }

        var pipeline = ValidatorPipeline.FromManifest(manifest);

        return new ProblemPackage
        {
            PackageDirectory = root,
            Manifest = manifest,
            Description = description!,
            Pipeline = pipeline,
            ValidatorConfigJson = pipeline.ToJson(),
            EditableFiles = workspace.Editable,
            ReadonlyFiles = workspace.Readonly,
        };
    }

    private const string FailureMessage = "The problem package is not valid.";

    private static string? ReadDescription(string root, ProblemManifest manifest, ManifestErrors errors)
    {
        if (!TryResolve(root, manifest.Description, out var path))
        {
            errors.Add("description", "The description path leaves the package.");
            return null;
        }

        if (!File.Exists(path))
        {
            errors.Add("description", $"No file at '{manifest.Description}'.");
            return null;
        }

        var text = File.ReadAllText(path);

        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add("description", "The description is empty.");
        }

        return text;
    }

    private static WorkspaceClassification ClassifyWorkspace(
        string root,
        ProblemManifest manifest,
        ManifestErrors errors)
    {
        var editableMatcher = MatcherFor(manifest.Workspace.Editable);
        var empty = new WorkspaceClassification([], [], editableMatcher);

        if (!TryResolve(root, manifest.Workspace.Root, out var workspaceRoot))
        {
            errors.Add("workspace.root", "The workspace root leaves the package.");
            return empty;
        }

        if (!Directory.Exists(workspaceRoot))
        {
            errors.Add("workspace.root", $"No directory at '{manifest.Workspace.Root}'.");
            return empty;
        }

        var files = new List<string>();
        var totalBytes = 0L;
        var limits = manifest.Limits;

        foreach (var entry in new DirectoryInfo(workspaceRoot)
            .EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            var relative = Relative(workspaceRoot, entry.FullName);

            // A link is a path that resolves differently on the machine that follows it, which is
            // the opposite of the reproducibility a package is required to have.
            if (entry.LinkTarget is not null)
            {
                errors.Add("workspace.root", $"'{relative}' is a link; packages hold real files only.");
                continue;
            }

            if (entry is not FileInfo file)
            {
                continue;
            }

            files.Add(relative);
            totalBytes += file.Length;

            if (file.Length > limits.MaxFileBytes)
            {
                errors.Add(
                    "limits.max_file_bytes",
                    $"'{relative}' is {file.Length} bytes, over the {limits.MaxFileBytes} allowed.");
            }
        }

        if (files.Count == 0)
        {
            errors.Add("workspace.root", "The workspace root holds no files.");
            return empty;
        }

        if (files.Count > limits.MaxFiles)
        {
            errors.Add("limits.max_files", $"The workspace holds {files.Count} files, over the {limits.MaxFiles} allowed.");
        }

        if (totalBytes > limits.MaxTotalBytes)
        {
            errors.Add("limits.max_total_bytes", $"The workspace is {totalBytes} bytes, over the {limits.MaxTotalBytes} allowed.");
        }

        files.Sort(StringComparer.Ordinal);

        var editable = Match(editableMatcher, files);
        var readOnly = Match(MatcherFor(manifest.Workspace.Readonly), files);

        // Neither list is a default: a file nobody classified would have its permission decided by
        // omission, and a file in both would have it decided by matching order.
        foreach (var file in files)
        {
            switch (editable.Contains(file), readOnly.Contains(file))
            {
                case (false, false):
                    errors.Add("workspace", $"'{file}' is matched by neither editable nor readonly.");
                    break;
                case (true, true):
                    errors.Add("workspace", $"'{file}' is matched by both editable and readonly.");
                    break;
                default:
                    break;
            }
        }

        if (editable.Count == 0)
        {
            errors.Add("workspace.editable", "No file in the workspace is editable.");
        }

        return new WorkspaceClassification(
            [.. files.Where(editable.Contains)],
            [.. files.Where(f => readOnly.Contains(f) && !editable.Contains(f))],
            editableMatcher);
    }

    private static void CheckFixtures(
        string root,
        ProblemManifest manifest,
        Matcher editableMatcher,
        ManifestErrors errors)
    {
        CheckFixture(root, manifest.Fixtures.Passing, "fixtures.passing", editableMatcher, errors);
        CheckFixture(root, manifest.Fixtures.Failing, "fixtures.failing", editableMatcher, errors);
    }

    private static void CheckFixture(
        string root,
        string? fixtureDirectory,
        string field,
        Matcher editableMatcher,
        ManifestErrors errors)
    {
        if (fixtureDirectory is null)
        {
            return;
        }

        if (!TryResolve(root, fixtureDirectory, out var path))
        {
            errors.Add(field, "The fixture path leaves the package.");
            return;
        }

        if (!Directory.Exists(path))
        {
            errors.Add(field, $"No directory at '{fixtureDirectory}'.");
            return;
        }

        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Select(file => Relative(path, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            errors.Add(field, "The fixture holds no files.");
            return;
        }

        // A fixture is an answer, so it may only touch what an answer could touch. One that edits a
        // read-only file proves nothing about the task a user is actually given.
        var editable = Match(editableMatcher, files);

        foreach (var file in files.Where(file => !editable.Contains(file)))
        {
            errors.Add(field, $"'{file}' is not an editable workspace path.");
        }
    }

    private static Matcher MatcherFor(string[] globs)
    {
        var matcher = new Matcher(StringComparison.Ordinal);

        foreach (var glob in globs)
        {
            matcher.AddInclude(glob);
        }

        return matcher;
    }

    private static HashSet<string> Match(Matcher matcher, IReadOnlyCollection<string> files) =>
        [.. matcher.Match(files).Files.Select(match => match.Path)];

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Resolves a manifest path against the package root, refusing anything that escapes it.</summary>
    private static bool TryResolve(string root, string relative, out string resolved)
    {
        resolved = Path.GetFullPath(Path.Combine(root, relative));

        return resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private sealed record WorkspaceClassification(
        IReadOnlyList<string> Editable,
        IReadOnlyList<string> Readonly,
        Matcher EditableMatcher);
}
