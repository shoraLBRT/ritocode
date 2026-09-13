using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Submissions.Domain;
using Ritocode.Shared.Persistence;
using Ritocode.Shared.Storage;

namespace Ritocode.Modules.Submissions.Persistence.Configurations;

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("submissions", table =>
        {
            table.HasEnumCheckConstraint<Submission, SubmissionStatus>("status");

            table.HasCheckConstraint(
                "ck_submissions_score_range",
                $"score IS NULL OR (score >= {Submission.MinScore} AND score <= {Submission.MaxScore})");

            // A terminal submission has a completion time and a non-terminal one does not.
            // Enforced here because a worker crashing mid-transition is exactly how this
            // invariant would otherwise rot.
            table.HasCheckConstraint(
                "ck_submissions_completed_at_matches_status",
                "(status IN ('Completed', 'Failed')) = (completed_at IS NOT NULL)");

            // A queued attempt has never been claimed; a running or completed one has been. A failed one
            // may have failed in the queue or during a run, so it may be either. The claim time is what
            // the queue's recording guard compares, so a row without one where a claim must exist would
            // be an attempt no worker could ever finish.
            table.HasCheckConstraint(
                "ck_submissions_started_at_matches_status",
                "status = 'Failed' OR ((status = 'Queued') = (started_at IS NULL))");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.WorkspaceId).IsRequired();
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Where the frozen input tree is, stored rather than derived from the id, so the layout can
        // move without stranding a row (docs/STORAGE_LAYOUT.md rule 3). Typed, as the other two
        // reference columns are.
        builder.Property(s => s.InputReference)
            .HasConversion<StorageReferenceConverter>()
            .HasMaxLength(StorageReference.MaxLength).IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();

        // Attempt history for a user, newest first.
        builder.HasIndex(s => new { s.UserId, s.CreatedAt }).IsDescending(false, true);

        builder.HasIndex(s => s.WorkspaceId);

        // The queue drain query. Partial, so the index stays the size of the backlog rather
        // than the size of all history.
        builder.HasIndex(s => new { s.Status, s.CreatedAt })
            .HasFilter("status IN ('Queued', 'Running')");
    }
}
