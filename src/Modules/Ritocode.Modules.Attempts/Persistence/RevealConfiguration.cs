using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

internal sealed class RevealConfiguration : IEntityTypeConfiguration<Reveal>
{
    public const string TableName = "reveals";

    public void Configure(EntityTypeBuilder<Reveal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        // One per learner and task: a second reveal says nothing more, and one racing the first is
        // refused here and taken as already revealed.
        builder.HasKey(reveal => new { reveal.UserId, reveal.TaskSlug });

        builder.Property(reveal => reveal.TaskSlug).HasMaxLength(AttemptConfiguration.SlugMaxLength).IsRequired();
        builder.Property(reveal => reveal.RevealedAt).IsRequired();
    }
}
