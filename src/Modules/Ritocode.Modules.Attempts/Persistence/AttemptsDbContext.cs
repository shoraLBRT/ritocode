using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>
/// The <c>attempts</c> schema: attempts and signals. Every row belongs to a user; see
/// <see cref="OwnedAttempts"/> and <see cref="OwnedSignals"/>.
/// </summary>
public sealed class AttemptsDbContext(DbContextOptions<AttemptsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "attempts";

    public override string Schema => SchemaName;

    public DbSet<Attempt> Attempts => Set<Attempt>();

    public DbSet<Signal> Signals => Set<Signal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AttemptConfiguration());
        modelBuilder.ApplyConfiguration(new SignalConfiguration());
    }
}
