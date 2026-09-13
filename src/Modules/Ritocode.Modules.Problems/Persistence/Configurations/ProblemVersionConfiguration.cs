using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Shared.Persistence;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Problems.Persistence.Configurations;

internal sealed class ProblemVersionConfiguration : IEntityTypeConfiguration<ProblemVersion>
{
    public void Configure(EntityTypeBuilder<ProblemVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("problem_versions", table =>
        {
            table.HasCheckConstraint("ck_problem_versions_version_positive", "version >= 1");

            // The format's own rule (docs/PROBLEM_PACKAGE_SPEC.md, Limits), restated where a write that
            // bypassed the loader would otherwise store a limit no workspace could ever satisfy.
            table.HasCheckConstraint(
                "ck_problem_versions_limits_valid",
                "max_files >= 1 AND max_file_bytes >= 1 AND max_total_bytes >= max_file_bytes");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ProblemId).IsRequired();
        builder.Property(v => v.Version).IsRequired();
        // Typed rather than a bare string: the column can then only ever hold the role/key form of
        // docs/STORAGE_LAYOUT.md, and a value a later build cannot resolve fails where it is read
        // instead of somewhere downstream that assumed it parsed. See StorageReferenceConverter.
        builder.Property(v => v.SnapshotReference)
            .HasConversion<StorageReferenceConverter>()
            .HasMaxLength(StorageReference.MaxLength).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();

        // jsonb rather than text: it is validated on write by PostgreSQL and can be queried
        // directly when diagnosing an evaluation, which a text blob cannot.
        builder.Property(v => v.ValidatorConfig).HasColumnType("jsonb").IsRequired();

        // Unbounded text, like the description: the manifest format puts no length on a path, and
        // a limit invented here would reject a valid package at insert time rather than at load.
        builder.Property(v => v.WorkspaceRoot).IsRequired();

        // text[], as problems.tags is: a short list read whole, with no attributes of its own. Read by
        // another module through IWorkspaceAllowanceLookup, never queried by element.
        builder.Property(v => v.EditableFiles).HasColumnType("text[]").IsRequired();
        builder.Property(v => v.MaxFiles).IsRequired();
        builder.Property(v => v.MaxFileBytes).IsRequired();
        builder.Property(v => v.MaxTotalBytes).IsRequired();

        // Same module, so this is a real foreign key. Deleting a problem takes its versions with
        // it; a version without its problem has no meaning.
        builder.HasOne(v => v.Problem)
            .WithMany()
            .HasForeignKey(v => v.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.ProblemId, v.Version }).IsUnique();

        // Resolving "the current version of this problem" is the catalog's hottest query.
        // Partial, because draft versions are never resolved and would only widen the index.
        builder.HasIndex(v => new { v.ProblemId, v.PublishedAt })
            .HasFilter("published_at IS NOT NULL");
    }
}
