using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Attempts.Persistence;

/// <summary>The <c>attempts</c> schema. Every row belongs to a user; see <see cref="OwnedAttempts"/>.</summary>
public sealed class AttemptsDbContext(DbContextOptions<AttemptsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "attempts";

    public override string Schema => SchemaName;

    public DbSet<Attempt> Attempts => Set<Attempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AttemptConfiguration());
    }
}
