using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Modules.Auth.Persistence.Configurations;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Auth.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "auth";

    public override string Schema => SchemaName;

    public DbSet<LinkedAccount> LinkedAccounts => Set<LinkedAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new LinkedAccountConfiguration());
    }
}
