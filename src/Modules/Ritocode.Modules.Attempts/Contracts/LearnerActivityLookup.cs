using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Shared.Contracts.Attempts;

namespace Ritocode.Modules.Attempts.Contracts;

/// <summary>The Attempts module's answer to <see cref="ILearnerActivityLookup"/>, over its own schema.</summary>
internal sealed class LearnerActivityLookup(AttemptsDbContext context) : ILearnerActivityLookup
{
    public async Task<LearnerActivity> ReadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        // One query after another: a DbContext runs one at a time.
        var started = await context.EveryonesAttempts()
            .Where(attempt => attempt.StartedAt >= from && attempt.StartedAt < to)
            .Select(attempt => new LearnerMoment(attempt.UserId, attempt.StartedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var submitted = await context.EveryonesAttempts()
            .Where(attempt => attempt.SubmittedAt != null && attempt.SubmittedAt >= from && attempt.SubmittedAt < to)
            .Select(attempt => new LearnerMoment(attempt.UserId, attempt.SubmittedAt!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var revealed = await context.EveryonesReveals()
            .Where(reveal => reveal.RevealedAt >= from && reveal.RevealedAt < to)
            .Select(reveal => new LearnerMoment(reveal.UserId, reveal.RevealedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signals = await context.EveryonesSignals()
            .Where(signal => signal.CreatedAt >= from && signal.CreatedAt < to)
            .Select(signal => new LearnerMoment(signal.UserId, signal.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studied = await context.EveryonesStudyMarks()
            .Where(mark => mark.StudiedAt >= from && mark.StudiedAt < to)
            .Select(mark => new LearnerMoment(mark.UserId, mark.StudiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LearnerActivity(started, submitted, revealed, signals, studied);
    }
}
