using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Modules.Problems.Persistence.Configurations;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Problems.Persistence;

public sealed class ProblemsDbContext(DbContextOptions<ProblemsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "problems";

    public override string Schema => SchemaName;

    public DbSet<Problem> Problems => Set<Problem>();

    public DbSet<ProblemVersion> ProblemVersions => Set<ProblemVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ProblemConfiguration());
        modelBuilder.ApplyConfiguration(new ProblemVersionConfiguration());
    }
}
