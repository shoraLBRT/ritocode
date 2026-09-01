using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Problems.Domain;

namespace Ritocode.Modules.Problems.Persistence.Configurations;

internal sealed class ProblemVersionConfiguration : IEntityTypeConfiguration<ProblemVersion>
{
    public void Configure(EntityTypeBuilder<ProblemVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("problem_versions", table => table.HasCheckConstraint(
            "ck_problem_versions_version_positive", "version >= 1"));

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ProblemId).IsRequired();
        builder.Property(v => v.Version).IsRequired();
        builder.Property(v => v.SnapshotReference)
            .HasMaxLength(ProblemVersion.SnapshotReferenceMaxLength).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();

        // jsonb rather than text: it is validated on write by PostgreSQL and can be queried
        // directly when diagnosing an evaluation, which a text blob cannot.
        builder.Property(v => v.ValidatorConfig).HasColumnType("jsonb").IsRequired();

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
