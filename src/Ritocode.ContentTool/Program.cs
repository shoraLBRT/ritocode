using Ritocode.Modules.Content.Authoring;
using Ritocode.Modules.Content.Catalogue;
using Ritocode.Modules.Content.Format;

// Exit codes: 0 — done (warnings are printed but never fail); 1 — the content has errors, or the task
// does not exist; 2 — the command line was not understood.
const string Usage = """
    usage: content validate [path]                 check a content tree (path defaults to 'content')
           content learner-view <task-slug> [path]  print a task exactly as a learner receives it
           content export <file> [path]              write what the frontend prerenders the public pages from
    """;

return args switch
{
    ["validate"] => Validate("content"),
    ["validate", var root] => Validate(root),
    ["learner-view", var task] => PrintLearnerView(task, "content"),
    ["learner-view", var task, var root] => PrintLearnerView(task, root),
    ["export", var file] => Export(file, "content"),
    ["export", var file, var root] => Export(file, root),
    _ => UsageError(),
};

static int Validate(string root)
{
    var (content, report) = ContentLoader.Load(root);

    foreach (var issue in report.Issues.OrderBy(issue => issue.Severity).ThenBy(issue => issue.Path, StringComparer.Ordinal))
    {
        (issue.Severity == ContentSeverity.Error ? Console.Error : Console.Out).WriteLine(issue);
    }

    var errors = report.Errors.Count();
    var warnings = report.Warnings.Count();

    Console.WriteLine(
        $"{Path.GetFullPath(root)}: {content.Cards.Count} cards, {content.Materials.Count} materials, "
        + $"{content.Tasks.Count} tasks — {errors} error(s), {warnings} warning(s).");

    return report.HasErrors ? 1 : 0;
}

// Only the learner's view goes to standard output, so it can be redirected straight into the smoke
// test; anything else goes to standard error. Content with errors is refused: a view rendered from
// it would not be what ingest serves.
static int PrintLearnerView(string task, string root)
{
    var (content, report) = ContentLoader.Load(root);

    if (report.HasErrors)
    {
        foreach (var issue in report.Errors.OrderBy(issue => issue.Path, StringComparer.Ordinal))
        {
            Console.Error.WriteLine(issue);
        }

        Console.Error.WriteLine("The content has errors; run 'content validate' and fix them first.");
        return 1;
    }

    var view = LearnerView.Render(content, task);

    if (view is null)
    {
        Console.Error.WriteLine($"There is no task '{task}' in {Path.GetFullPath(root)}.");
        return 1;
    }

    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.Out.Write(view);
    return 0;
}

// Refused for content with errors, as ingest refuses it: the static pages show what the API serves.
static int Export(string file, string root)
{
    var (content, report) = ContentLoader.Load(root);

    if (report.HasErrors)
    {
        foreach (var issue in report.Errors.OrderBy(issue => issue.Path, StringComparer.Ordinal))
        {
            Console.Error.WriteLine(issue);
        }

        Console.Error.WriteLine("The content has errors; run 'content validate' and fix them first.");
        return 1;
    }

    var export = ContentExport.From(content);
    var directory = Path.GetDirectoryName(Path.GetFullPath(file));

    if (directory is not null)
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(file, export.ToJson(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"{Path.GetFullPath(file)}: {export.Problems.Classes.Count} classes, {export.Problems.Cards.Count} cards.");
    return 0;
}

static int UsageError()
{
    Console.Error.WriteLine(Usage);
    return 2;
}
