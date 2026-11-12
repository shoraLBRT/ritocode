using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Attempts.Progress;

/// <summary>A user's progress, read from their own first attempts and the answers they have revealed.</summary>
public interface IProgressReader
{
    Task<ProgressView> GetAsync(Guid userId, CancellationToken cancellationToken);
}

/// <remarks>
/// Attempts depends on Content here for what it says of the cards: the class of each, and the names
/// (ADR 0007 §1). The stored
/// results are read as they were scored; progress is computed on read, so it follows the results
/// and never needs rewriting.
/// </remarks>
internal sealed class ProgressReader(AttemptsDbContext context, ICardClassLookup cards) : IProgressReader
{
    public async Task<ProgressView> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stored = await context.OwnedBy(userId)
            .Where(attempt => attempt.CountsTowardProgress && attempt.Result != null)
            .Select(attempt => new { attempt.TaskSlug, Result = attempt.Result! })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The tasks whose answer the learner has revealed: only their first attempts name cards (ADR 0017).
        var revealed = (await context.RevealsOf(userId)
            .Select(reveal => reveal.TaskSlug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        var firstAttempts = stored
            .Select(attempt => new FirstAttempt(AttemptsJson.Read<DiagnosisScore>(attempt.Result), revealed.Contains(attempt.TaskSlug)))
            .ToList();
        var slugs = firstAttempts.SelectMany(attempt => attempt.Score.Cards).Select(line => line.Card).Distinct(StringComparer.Ordinal).ToList();
        var catalogue = await cards.FindAsync(slugs, cancellationToken).ConfigureAwait(false);

        return ProgressCalculator.Calculate(firstAttempts, catalogue);
    }
}
