using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ritocode.Modules.Problems.Packaging;
using Ritocode.Modules.Problems.Persistence;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Problems.Ingest;

/// <summary>
/// Ingests the packages in a content directory once, after the host has started, so a local run
/// has a catalog to browse.
/// </summary>
/// <remarks>
/// <para>
/// This is the first caller of <see cref="IProblemIngest"/> and a development convenience, not the
/// content pipeline: it is off unless configuration turns it on, and it publishes a slug's first
/// version only. A package whose slug already has a published version is skipped rather than
/// ingested again — otherwise every restart would add a revision, and the catalog would report a
/// version number that counts restarts.
/// </para>
/// <para>
/// A <see cref="BackgroundService"/> rather than a blocking startup step, and every failure is
/// logged rather than thrown: a development seeding step that stops the host from serving is worse
/// than an empty catalog, and an invalid package is the author's to fix, not the host's to die on.
/// </para>
/// </remarks>
internal sealed partial class ProblemContentSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<ProblemContentOptions> options,
    IHostEnvironment environment,
    ILogger<ProblemContentSeeder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SeedOnStartup)
        {
            return;
        }

        var directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.Directory));

        if (!Directory.Exists(directory))
        {
            LogNoContentDirectory(logger, directory);
            return;
        }

        foreach (var packageDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await SeedAsync(packageDirectory, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSeedFailed(logger, packageDirectory, exception);
            }
        }
    }

    private async Task SeedAsync(string packageDirectory, CancellationToken cancellationToken)
    {
        var loaded = ProblemPackageLoader.Load(packageDirectory);

        if (!loaded.IsSuccess)
        {
            LogInvalidPackage(logger, packageDirectory, loaded.Error.Message);
            return;
        }

        var package = loaded.Value;
        var slug = package.Manifest.Slug.Trim().ToLowerInvariant();

        // A scope per package: the seeder is a singleton and the DbContext is scoped, and one
        // context shared across every package would keep each ingested graph tracked for the life
        // of the host.
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProblemsDbContext>();

        var alreadyPublished = await context.ProblemVersions
            .AnyAsync(
                version => version.PublishedAt != null && version.Problem!.Slug == slug,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyPublished)
        {
            LogAlreadyPublished(logger, slug);
            return;
        }

        var ingest = scope.ServiceProvider.GetRequiredService<IProblemIngest>();
        var ingested = await ingest.IngestAsync(package, cancellationToken).ConfigureAwait(false);

        LogIngested(logger, ingested.Slug, ingested.Version, ingested.Bundle);
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Published problem {Slug} version {Version}, bundle at {Bundle}")]
    private static partial void LogIngested(ILogger logger, string slug, int version, StorageReference bundle);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Problem {Slug} already has a published version; leaving it alone")]
    private static partial void LogAlreadyPublished(ILogger logger, string slug);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "No problem content directory at {Directory}; nothing was seeded")]
    private static partial void LogNoContentDirectory(ILogger logger, string directory);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "The package at {PackageDirectory} is not valid and was not ingested: {Reason}")]
    private static partial void LogInvalidPackage(ILogger logger, string packageDirectory, string reason);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Error,
        Message = "Ingesting the package at {PackageDirectory} failed")]
    private static partial void LogSeedFailed(ILogger logger, string packageDirectory, Exception exception);
}
