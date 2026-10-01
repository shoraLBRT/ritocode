using Microsoft.Extensions.DependencyInjection;
using Ritocode.Modules.Content.Ingest;

namespace Ritocode.DbMigrator;

/// <summary>
/// <c>ingest &lt;root&gt; &lt;revision&gt;</c>: the release's content step (docs/SPEC.md §7.3, #136). The
/// content tree ships in the API image beside this tool, so a release ingests exactly the content of
/// the commit it deploys, stamped with that commit. Content with errors is refused and nothing is
/// written — ingest validates first, in one transaction.
/// </summary>
internal static class ContentIngestCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string root, string revision)
    {
        await using var scope = services.CreateAsyncScope();
        var ingest = scope.ServiceProvider.GetRequiredService<IContentIngest>();

        var result = await ingest.IngestAsync(Path.GetFullPath(root), revision, CancellationToken.None).ConfigureAwait(false);

        foreach (var issue in result.Report.Issues)
        {
            Console.Error.WriteLine(issue);
        }

        if (!result.Ingested)
        {
            Console.Error.WriteLine($"The content at {Path.GetFullPath(root)} has errors; nothing was ingested.");
            return ExitCodes.Failure;
        }

        Console.WriteLine(
            $"Ingested {revision}: {result.Cards} cards, {result.Materials} materials, {result.Tasks} tasks; "
            + $"{result.CardsRetired} cards retired, {result.TasksUnpublished} tasks unpublished.");

        return ExitCodes.Success;
    }
}
