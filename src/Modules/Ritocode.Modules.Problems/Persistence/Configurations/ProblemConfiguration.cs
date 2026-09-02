using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Problems.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Problems.Persistence.Configurations;

internal sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("problems",
            table => table.HasEnumCheckConstraint<Problem, Difficulty>("difficulty"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Slug).HasMaxLength(Problem.SlugMaxLength).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(Problem.TitleMaxLength).IsRequired();
        builder.Property(p => p.Difficulty).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Description).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        // text[] rather than a join table: tags are a closed, short list per problem with no
        // attributes of their own, and PostgreSQL indexes arrays well enough for catalog filters.
        builder.Property(p => p.Tags).HasColumnType("text[]").IsRequired();

        builder.HasIndex(p => p.Slug).IsUnique();

        // GIN over the array supports the containment queries catalog filtering uses
        // (WHERE tags @> ARRAY['refactoring']).
        builder.HasIndex(p => p.Tags).HasMethod("gin");

        builder.HasIndex(p => p.Difficulty);
    }
}
