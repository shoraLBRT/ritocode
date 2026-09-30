using Ritocode.Modules.Content.Format;

namespace Ritocode.Modules.Content.Ingest;

/// <summary>
/// What the task screen shows above the file tree (docs/SPEC.md §4.4): enough to see a project's
/// shape — and its disproportion — without reading every line. Derived at ingest, never authored.
/// </summary>
public sealed record MaterialOverview(
    IReadOnlyList<OverviewFile> Files,
    int TotalLines,
    int FileCount,
    IReadOnlyList<string> Dependencies)
{
    public static MaterialOverview Of(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        return new MaterialOverview(
            [.. material.Files.Select(file => new OverviewFile(file.Path, file.LineCount))],
            material.TotalLines,
            material.Files.Count,
            DeclaredDependencies(material));
    }

    /// <summary>
    /// Package names declared in <c>requirements.txt</c> or under <c>[project] dependencies</c> in
    /// <c>pyproject.toml</c> at the root of the material, without versions, ordered and distinct.
    /// </summary>
    private static List<string> DeclaredDependencies(Material material)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in material.Files)
        {
            var lines = file.Content.Split('\n');

            if (file.Path == "requirements.txt")
            {
                foreach (var line in lines)
                {
                    AddRequirement(names, line.Split('#')[0]);
                }
            }
            else if (file.Path == "pyproject.toml")
            {
                foreach (var requirement in ProjectDependencies(lines))
                {
                    AddRequirement(names, requirement);
                }
            }
        }

        return [.. names];
    }

    private static IEnumerable<string> ProjectDependencies(string[] lines)
    {
        var inProject = false;
        var inList = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (!inList && line.StartsWith('['))
            {
                inProject = line == "[project]";
                continue;
            }

            if (inProject && !inList && line.StartsWith("dependencies", StringComparison.Ordinal) && line.Contains('[', StringComparison.Ordinal))
            {
                inList = true;
                line = line[(line.IndexOf('[', StringComparison.Ordinal) + 1)..];
            }

            if (!inList)
            {
                continue;
            }

            var closes = line.Contains(']', StringComparison.Ordinal);

            foreach (var part in line.Split(','))
            {
                var value = part.Trim().TrimEnd(']').Trim().Trim('"', '\'');

                if (value.Length > 0)
                {
                    yield return value;
                }
            }

            if (closes)
            {
                inList = false;
            }
        }
    }

    private static void AddRequirement(SortedSet<string> names, string requirement)
    {
        var text = requirement.Trim();

        if (text.Length == 0 || text.StartsWith('-'))
        {
            return;
        }

        var end = text.IndexOfAny(['=', '<', '>', '!', '~', '[', ';', '@', ' ']);
        var name = (end < 0 ? text : text[..end]).Trim();

        if (name.Length > 0)
        {
            names.Add(name);
        }
    }
}

public sealed record OverviewFile(string Path, int Lines);
