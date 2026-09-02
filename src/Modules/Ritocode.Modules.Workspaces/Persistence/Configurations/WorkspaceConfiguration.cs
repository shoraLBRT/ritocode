using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Workspaces.Domain;

namespace Ritocode.Modules.Workspaces.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("workspaces", table => table.HasCheckConstraint(
            "ck_workspaces_updated_not_before_created", "updated_at >= created_at"));

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.UserId).IsRequired();
        builder.Property(w => w.ProblemVersionId).IsRequired();
        builder.Property(w => w.SnapshotReference)
            .HasMaxLength(Workspace.SnapshotReferenceMaxLength).IsRequired();
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();

        // Cross-module id columns get explicit indexes: without a foreign key constraint they
        // do not inherit one, and both are lookup keys.
        builder.HasIndex(w => new { w.UserId, w.ProblemVersionId });
        builder.HasIndex(w => w.ProblemVersionId);

        // "My recent workspaces", descending by last write.
        builder.HasIndex(w => new { w.UserId, w.UpdatedAt })
            .IsDescending(false, true);
    }
}
