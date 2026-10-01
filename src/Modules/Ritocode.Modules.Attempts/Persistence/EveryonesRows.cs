using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>
/// Every user's attempts and signals, for the admin area (docs/SPEC.md §6.2) and the tallies of its list
/// of users — the one place in the module where the owner is deliberately not in the query.
/// </summary>
/// <remarks>
/// Called only from <c>AdminReader</c>, whose endpoints are mapped behind the admin policy, and from
/// <c>AttemptTallyLookup</c>, which answers counts for the Users module's admin list and nothing about
/// a row. <c>OwnershipRuleTests</c> names this class as an allowance with that reason.
/// </remarks>
internal static class EveryonesRows
{
    /// <summary>Every attempt, untracked and unordered.</summary>
    public static IQueryable<Attempt> EveryonesAttempts(this AttemptsDbContext context) =>
        context.Attempts.AsNoTracking();

    /// <summary>Every signal, untracked and unordered.</summary>
    public static IQueryable<Signal> EveryonesSignals(this AttemptsDbContext context) =>
        context.Signals.AsNoTracking();

    /// <summary>The signal <paramref name="signalId"/>, tracked, whoever sent it.</summary>
    public static Task<Signal?> FindAnySignalAsync(this AttemptsDbContext context, Guid signalId, CancellationToken cancellationToken) =>
        context.Signals.FirstOrDefaultAsync(candidate => candidate.Id == signalId, cancellationToken);
}
