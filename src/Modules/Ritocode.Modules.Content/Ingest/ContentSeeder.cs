using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ritocode.Modules.Content.Ingest;

/// <summary>
/// Whether the host loads <c>content/</c> into the database when it starts. On in Development only:
/// production content arrives through the release, stamped with the commit it came from.
/// </summary>
public sealed class ContentSeedOptions
{
    public const string SectionName = "Content:Seed";

    public bool Enabled { get; init; }

    /// <summary>The content root, relative to the host's content root.</summary>
    [Required]
    public string Directory { get; init; } = "content";
}

/// <summary>
/// Loads <c>content/</c> into the database when a development host starts, so an edit to a card or
/// a task shows up on the next run. Content with errors is reported and not written.
/// </summary>
internal sealed partial class ContentSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<ContentSeedOptions> options,
    IHostEnvironment environment,
    ILogger<ContentSeeder> logger) : BackgroundService
{
    public const string Revision = "development";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.Directory));

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var ingest = scope.ServiceProvider.GetRequiredService<IContentIngest>();
            var result = await ingest.IngestAsync(root, Revision, stoppingToken).ConfigureAwait(false);

            foreach (var issue in result.Report.Issues)
            {
                LogIssue(logger, issue.ToString());
            }

            if (result.Ingested)
            {
                LogIngested(logger, root, result.Cards, result.Materials, result.Tasks, result.CardsRetired, result.TasksUnpublished);
            }
            else
            {
                LogRefused(logger, root);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(logger, root, exception);
        }
    }

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Seeded content from {Root}: {Cards} cards, {Materials} materials, {Tasks} tasks; {Retired} cards retired, {Unpublished} tasks unpublished")]
    private static partial void LogIngested(ILogger logger, string root, int cards, int materials, int tasks, int retired, int unpublished);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Content: {Issue}")]
    private static partial void LogIssue(ILogger logger, string issue);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "The content at {Root} has errors and was not seeded")]
    private static partial void LogRefused(ILogger logger, string root);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Error, Message = "Seeding the content at {Root} failed")]
    private static partial void LogFailed(ILogger logger, string root, Exception exception);
}
