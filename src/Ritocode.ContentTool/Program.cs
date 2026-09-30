using Ritocode.Modules.Problems.ContentFormat;

// Exit codes: 0 — no errors (warnings are printed but never fail); 1 — the content has errors;
// 2 — the command line was not understood.
const string Usage = "usage: content validate [path]   (path defaults to 'content')";

if (args is not ["validate", ..] || args.Length > 2)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var root = args.Length == 2 ? args[1] : "content";
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
