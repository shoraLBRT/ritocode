using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Errors.Domain;

namespace Ritocode.Modules.Errors.Persistence.Configurations;

internal sealed class ErrorOccurrenceConfiguration : IEntityTypeConfiguration<ErrorOccurrence>
{
    public void Configure(EntityTypeBuilder<ErrorOccurrence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("error_occurrences");

        builder.HasKey(occurrence => occurrence.Id);
        builder.Property(occurrence => occurrence.Id).ValueGeneratedNever();

        builder.Property(occurrence => occurrence.GroupId).IsRequired();
        builder.Property(occurrence => occurrence.OccurredAt).IsRequired();
        builder.Property(occurrence => occurrence.RequestId).HasMaxLength(ErrorOccurrence.RequestIdMaxLength);
        builder.Property(occurrence => occurrence.Message).HasMaxLength(ErrorGroup.MessageMaxLength).IsRequired();
        builder.Property(occurrence => occurrence.Location).HasMaxLength(ErrorGroup.LocationMaxLength).IsRequired();
        builder.Property(occurrence => occurrence.Stack).HasMaxLength(ErrorOccurrence.StackMaxLength);
        builder.Property(occurrence => occurrence.Commit).HasMaxLength(ErrorOccurrence.CommitMaxLength);

        // Same schema, so a real foreign key; the occurrences go with their group.
        builder.HasOne<ErrorGroup>().WithMany().HasForeignKey(occurrence => occurrence.GroupId).OnDelete(DeleteBehavior.Cascade);

        // A group's latest occurrences, and the pruning of all but its latest 50.
        builder.HasIndex(occurrence => new { occurrence.GroupId, occurrence.OccurredAt });

        // The pruning of everything older than 30 days.
        builder.HasIndex(occurrence => occurrence.OccurredAt);
    }
}
