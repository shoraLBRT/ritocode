using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Attempts.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Attempts.Persistence;

internal sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    /// <summary>The longest slug the content format allows (docs/CONTENT_FORMAT.md §2).</summary>
    public const int SlugMaxLength = 64;

    public const string FirstSubmissionIndex = "ux_attempts_first_submission";

    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("attempts", table =>
        {
            table.HasEnumCheckConstraint<Attempt, AttemptStep>("step");

            // Everything a submit writes is written together, so a row is either open or submitted,
            // never half of each.
            table.HasCheckConstraint(
                "ck_attempts_submitted_whole",
                "(submitted_at IS NULL) = (content_revision IS NULL) AND (submitted_at IS NULL) = (answer IS NULL) "
                + "AND (submitted_at IS NULL) = (result IS NULL) AND (submitted_at IS NULL) = (score IS NULL) "
                + "AND (submitted_at IS NULL) = (max_score IS NULL)");

            table.HasCheckConstraint(
                "ck_attempts_counts_only_when_submitted",
                "NOT counts_toward_progress OR submitted_at IS NOT NULL");

            table.HasCheckConstraint(
                "ck_attempts_score_range",
                "score IS NULL OR (score >= 0 AND score <= max_score)");
        });

        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();

        builder.Property(attempt => attempt.UserId).IsRequired();
        builder.Property(attempt => attempt.TaskSlug).HasMaxLength(SlugMaxLength).IsRequired();
        builder.Property(attempt => attempt.StartedAt).IsRequired();
        builder.Property(attempt => attempt.Step).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(attempt => attempt.ContentRevision).HasMaxLength(64);
        builder.Property(attempt => attempt.Answer).HasColumnType("jsonb");
        builder.Property(attempt => attempt.Result).HasColumnType("jsonb");

        // A user's history, newest first.
        builder.HasIndex(attempt => new { attempt.UserId, attempt.StartedAt }).IsDescending(false, true);

        // The rate limit's count of a user's recent submits.
        builder.HasIndex(attempt => new { attempt.UserId, attempt.SubmittedAt });

        // A user's attempts at one task, and the catalogue's solved flags.
        // Named, as the next one is: two indexes over the same columns are otherwise one to EF.
        builder.HasIndex(attempt => new { attempt.UserId, attempt.TaskSlug }, "ix_attempts_user_id_task_slug");

        // One attempt per user and task counts toward progress. Two first submits racing each other
        // cannot both count: the second is refused here and saved as practice.
        builder.HasIndex(attempt => new { attempt.UserId, attempt.TaskSlug }, FirstSubmissionIndex)
            .HasDatabaseName(FirstSubmissionIndex)
            .IsUnique()
            .HasFilter("counts_toward_progress");
    }
}
