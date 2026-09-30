using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Modules.Attempts.Lifecycle;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Errors;

namespace Ritocode.Modules.Attempts.Signals;

/// <summary>Sends a signal from an extra pick (docs/SPEC.md §4.8). It never touches the attempt or its score.</summary>
public interface ISignalSender
{
    Task<Result<SignalView>> SendAsync(Guid userId, Guid attemptId, string card, string? comment, CancellationToken cancellationToken);
}

internal sealed class SignalSender(
    AttemptsDbContext context,
    IOptions<SignalRateLimitOptions> rateLimit,
    TimeProvider clock) : ISignalSender
{
    public const string NotSubmittedCode = "attempt_not_submitted";

    public const string AlreadySentCode = "signal_already_sent";

    public const string RateLimitedCode = "signal_rate_limited";

    public async Task<Result<SignalView>> SendAsync(Guid userId, Guid attemptId, string card, string? comment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(card);

        var now = Now();
        var limit = rateLimit.Value;

        // Counted over the rows, as the submission limit is; a burst racing past the cap is accepted.
        var windowStart = now - limit.Window;
        var recent = await context.SignalsOf(userId)
            .CountAsync(candidate => candidate.CreatedAt > windowStart, cancellationToken)
            .ConfigureAwait(false);

        if (recent >= limit.MaxSignals)
        {
            return AppError.RateLimited(
                RateLimitedCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"At most {limit.MaxSignals} signals can be sent in any {limit.Window:c}. Try again later."));
        }

        // Untracked: nothing here changes the attempt.
        var attempt = await context.OwnedBy(userId)
            .FirstOrDefaultAsync(candidate => candidate.Id == attemptId, cancellationToken)
            .ConfigureAwait(false);

        if (attempt is null)
        {
            return AttemptLifecycle.AttemptNotFound();
        }

        // Before the key is revealed there is no extra pick, and an answer about a card would say
        // whether the key lists it.
        if (attempt.Result is null)
        {
            return AppError.Conflict(NotSubmittedCode, "Signals are sent from the review of a submitted attempt.");
        }

        var extra = AttemptsJson.Read<DiagnosisScore>(attempt.Result).Cards
            .Any(line => line.Outcome == CardOutcome.Extra && string.Equals(line.Card, card, StringComparison.Ordinal));

        if (!extra)
        {
            return AppError.Validation(
                "A signal is sent for a card the attempt picked and the answer key does not list.",
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["card"] = [$"The card '{card}' is not an extra pick of this attempt."],
                });
        }

        var sent = await context.SignalsOf(userId)
            .AnyAsync(candidate => candidate.AttemptId == attemptId && candidate.Card == card, cancellationToken)
            .ConfigureAwait(false);

        if (sent)
        {
            return AlreadySent();
        }

        var signal = Signal.Send(attempt, card, comment, now);
        context.Signals.Add(signal);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsOnePerPickRace(exception))
        {
            return AlreadySent();
        }

        return new SignalView(signal.Id, signal.AttemptId, signal.TaskSlug, signal.Card, signal.Comment, signal.CreatedAt);
    }

    private static AppError AlreadySent() =>
        AppError.Conflict(AlreadySentCode, "This card has already been signalled from this attempt.");

    /// <summary>The clock, to the microsecond PostgreSQL keeps, as for attempts.</summary>
    private DateTimeOffset Now()
    {
        var now = clock.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static bool IsOnePerPickRace(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: SignalConfiguration.OnePerPickIndex,
        };
}
