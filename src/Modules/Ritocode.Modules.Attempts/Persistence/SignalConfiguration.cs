using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

internal sealed class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public const string OnePerPickIndex = "ux_signals_attempt_id_card";

    public void Configure(EntityTypeBuilder<Signal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("signals");

        builder.HasKey(signal => signal.Id);
        builder.Property(signal => signal.Id).ValueGeneratedNever();

        builder.Property(signal => signal.UserId).IsRequired();
        builder.Property(signal => signal.AttemptId).IsRequired();
        builder.Property(signal => signal.TaskSlug).HasMaxLength(AttemptConfiguration.SlugMaxLength).IsRequired();
        builder.Property(signal => signal.Card).HasMaxLength(AttemptConfiguration.SlugMaxLength).IsRequired();
        builder.Property(signal => signal.Comment).HasMaxLength(Signal.CommentMaxLength);
        builder.Property(signal => signal.CreatedAt).IsRequired();

        // Same schema, so the attempt is a real foreign key; a signal goes with its attempt.
        builder.HasOne<Attempt>().WithMany().HasForeignKey(signal => signal.AttemptId).OnDelete(DeleteBehavior.Cascade);

        // One signal per extra pick: sending it twice says nothing more. A second send racing the
        // first is refused here.
        builder.HasIndex(signal => new { signal.AttemptId, signal.Card })
            .HasDatabaseName(OnePerPickIndex)
            .IsUnique();

        // The rate limit's count of a user's recent signals.
        builder.HasIndex(signal => new { signal.UserId, signal.CreatedAt });
    }
}
