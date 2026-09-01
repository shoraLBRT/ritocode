using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Users.Domain;
using Ritocode.Modules.Users.Persistence.Configurations;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Users.Persistence;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "users";

    public override string Schema => SchemaName;

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
    }
}
