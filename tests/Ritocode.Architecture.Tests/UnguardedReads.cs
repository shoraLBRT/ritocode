using Microsoft.EntityFrameworkCore;

namespace Ritocode.Architecture.Tests;

/// <summary>
/// Never called. Each member reaches a user's rows without the owner in the query, in a different
/// shape, so <see cref="OwnershipRuleTests"/> can prove its reader sees every one of them — a rule that
/// silently stopped seeing would pass over the module code exactly as a clean one does.
/// </summary>
/// <remarks>
/// The rows are <see cref="ProofContext"/>'s rather than a module's, so the proof holds whether or not a
/// module owning a user's rows exists at the moment.
/// </remarks>
internal static class UnguardedReads
{
    public static IQueryable<ProofContext.OwnedRow> ThroughTheSetProperty(ProofContext context) =>
        context.Rows;

    public static IQueryable<ProofContext.OwnedNote> ThroughSet(ProofContext context) =>
        context.Set<ProofContext.OwnedNote>();

    public static ValueTask<ProofContext.OwnedRow?> ByKey(ProofContext context, Guid id) =>
        context.FindAsync<ProofContext.OwnedRow>(id);

    public static async Task<ProofContext.OwnedRow?> InsideAnAsyncMethod(ProofContext context, Guid id) =>
        await context.Rows.FirstOrDefaultAsync(row => row.Id == id);

    public static Func<ProofContext, IQueryable<ProofContext.OwnedNote>> InsideALambda() =>
        context => context.Notes;

    public static IQueryable<ProofContext.OwnedRow> ThroughRawSql(ProofContext context) =>
        context.Database.SqlQuery<ProofContext.OwnedRow>($"SELECT * FROM proof.rows");
}

/// <summary>A context that exists only to be read by <see cref="UnguardedReads"/>.</summary>
internal sealed class ProofContext(DbContextOptions<ProofContext> options) : DbContext(options)
{
    public DbSet<OwnedRow> Rows => Set<OwnedRow>();

    public DbSet<OwnedNote> Notes => Set<OwnedNote>();

    internal sealed class OwnedRow
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
    }

    internal sealed class OwnedNote
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
    }
}
