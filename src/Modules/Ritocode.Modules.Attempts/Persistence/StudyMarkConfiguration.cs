using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Attempts.Domain;

namespace Ritocode.Modules.Attempts.Persistence;

internal sealed class StudyMarkConfiguration : IEntityTypeConfiguration<StudyMark>
{
    public const string TableName = "study_marks";

    public void Configure(EntityTypeBuilder<StudyMark> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TableName);

        // One per learner and card: a second mark says nothing more, and one racing the first is
        // refused here and taken as already marked.
        builder.HasKey(mark => new { mark.UserId, mark.CardSlug });

        builder.Property(mark => mark.CardSlug).HasMaxLength(AttemptConfiguration.SlugMaxLength).IsRequired();
        builder.Property(mark => mark.StudiedAt).IsRequired();
    }
}
