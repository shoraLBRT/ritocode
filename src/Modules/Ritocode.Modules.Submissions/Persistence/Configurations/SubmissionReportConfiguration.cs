using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Submissions.Domain;

namespace Ritocode.Modules.Submissions.Persistence.Configurations;

internal sealed class SubmissionReportConfiguration : IEntityTypeConfiguration<SubmissionReport>
{
    public void Configure(EntityTypeBuilder<SubmissionReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("submission_reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.SubmissionId).IsRequired();
        builder.Property(r => r.ValidatorResults).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.LogsReference).HasMaxLength(SubmissionReport.LogsReferenceMaxLength);
        builder.Property(r => r.CreatedAt).IsRequired();

        // Same module, so a real foreign key. One report per submission; a second one would mean
        // the pipeline ran twice for the same attempt.
        builder.HasOne(r => r.Submission)
            .WithOne()
            .HasForeignKey<SubmissionReport>(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.SubmissionId).IsUnique();
    }
}
