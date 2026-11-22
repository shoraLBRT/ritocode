using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ritocode.Modules.Errors.Recording;

/// <summary>
/// Deletes occurrences past their retention every <see cref="ErrorsOptions.PruneInterval"/>, so a quiet site
/// does not keep them for ever: every new occurrence prunes too, but a site with no new errors writes none.
/// </summary>
internal sealed partial class ErrorPruner(ErrorRecorder recorder, IOptions<ErrorsOptions> options, TimeProvider clock, ILogger<ErrorPruner> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PruneInterval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var pruned = await recorder.PruneExpiredAsync(stoppingToken).ConfigureAwait(false);
                LogPruned(logger, pruned);
            }
            catch (Exception exception) when (exception is DbException or DbUpdateException or InvalidOperationException or TimeoutException)
            {
                LogNotPruned(logger, exception.GetType().Name);
            }
        }
    }

    [LoggerMessage(EventId = 4010, Level = LogLevel.Information, Message = "Pruned {Count} error occurrences past their retention")]
    private static partial void LogPruned(ILogger logger, int count);

    [LoggerMessage(EventId = 4011, Level = LogLevel.Warning, Message = "Error occurrences could not be pruned: {Failure}")]
    private static partial void LogNotPruned(ILogger logger, string failure);
}
