using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Workspaces.Domain;
using Ritocode.Modules.Workspaces.Persistence.Configurations;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Workspaces.Persistence;

public sealed class WorkspacesDbContext(DbContextOptions<WorkspacesDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "workspaces";

    public override string Schema => SchemaName;

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new WorkspaceConfiguration());
    }
}
