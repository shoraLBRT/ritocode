using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Modules.Errors.Persistence.Configurations;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Errors.Persistence;

/// <summary>The <c>errors</c> schema: error groups and their occurrences (ADR 0020 decision 1).</summary>
public sealed class ErrorsDbContext(DbContextOptions<ErrorsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "errors";

    public override string Schema => SchemaName;

    public DbSet<ErrorGroup> Groups => Set<ErrorGroup>();

    public DbSet<ErrorOccurrence> Occurrences => Set<ErrorOccurrence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ErrorGroupConfiguration());
        modelBuilder.ApplyConfiguration(new ErrorOccurrenceConfiguration());
    }
}
