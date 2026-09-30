using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Persistence;
using Ritocode.Modules.Attempts.Scoring;
using Ritocode.Shared.Contracts.Content;

namespace Ritocode.Modules.Attempts.Progress;

/// <summary>A user's progress, read from their own first attempts.</summary>
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
            .Select(attempt => attempt.Result!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var scores = stored.Select(AttemptsJson.Read<DiagnosisScore>).ToList();
        var slugs = scores.SelectMany(score => score.Cards).Select(line => line.Card).Distinct(StringComparer.Ordinal).ToList();
        var catalogue = await cards.FindAsync(slugs, cancellationToken).ConfigureAwait(false);

        return ProgressCalculator.Calculate(scores, catalogue);
    }
}
