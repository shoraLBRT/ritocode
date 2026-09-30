using Microsoft.EntityFrameworkCore;
using Ritocode.Modules.Content.Format;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Content.Persistence;

/// <summary>
/// The <c>content</c> schema. Written only by ingest; read by everything else.
/// </summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "content";

    public override string Schema => SchemaName;

    public DbSet<StoredTaxonomy> Taxonomy => Set<StoredTaxonomy>();

    public DbSet<StoredCard> Cards => Set<StoredCard>();

    public DbSet<StoredMaterial> Materials => Set<StoredMaterial>();

    public DbSet<StoredTask> Tasks => Set<StoredTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StoredTaxonomy>(taxonomy =>
        {
            taxonomy.ToTable("taxonomy", table => table.HasCheckConstraint("ck_taxonomy_singleton", $"id = {StoredTaxonomy.SingletonId}"));
            taxonomy.HasKey(row => row.Id);
            taxonomy.Property(row => row.Id).ValueGeneratedNever();
            taxonomy.Property(row => row.Document).HasColumnType("jsonb").IsRequired();
            taxonomy.Property(row => row.ContentRevision).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<StoredCard>(card =>
        {
            card.ToTable("cards", table => table.HasCheckConstraint(
                "ck_cards_weight_range",
                $"weight BETWEEN {ContentRules.MinWeight} AND {ContentRules.MaxWeight}"));
            card.HasKey(row => row.Slug);
            card.Property(row => row.Slug).HasMaxLength(ContentRules.SlugMaxLength);
            card.Property(row => row.Class).HasMaxLength(ContentRules.SlugMaxLength).IsRequired();
            card.Property(row => row.Texts).HasColumnType("jsonb").IsRequired();
            card.Property(row => row.ContentRevision).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<StoredMaterial>(material =>
        {
            material.ToTable("materials");
            material.HasKey(row => row.Slug);
            material.Property(row => row.Slug).HasMaxLength(ContentRules.SlugMaxLength);
            material.Property(row => row.Language).HasMaxLength(32).IsRequired();
            material.Property(row => row.Files).HasColumnType("jsonb").IsRequired();
            material.Property(row => row.Overview).HasColumnType("jsonb").IsRequired();
            material.Property(row => row.ContentRevision).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<StoredTask>(task =>
        {
            task.ToTable("tasks", table => table.HasCheckConstraint(
                "ck_tasks_difficulty",
                "difficulty IN ('easy', 'medium', 'hard')"));
            task.HasKey(row => row.Slug);
            task.Property(row => row.Slug).HasMaxLength(ContentRules.SlugMaxLength);
            task.Property(row => row.Material).HasMaxLength(ContentRules.SlugMaxLength).IsRequired();
            task.Property(row => row.Difficulty).HasMaxLength(16).IsRequired();
            task.Property(row => row.Findings).HasColumnType("jsonb").IsRequired();
            task.Property(row => row.Texts).HasColumnType("jsonb").IsRequired();
            task.Property(row => row.ContentRevision).HasMaxLength(64).IsRequired();

            // Tasks over one material are listed together in the review ("the same code in
            // another context"); the material has no foreign key because it is never deleted.
            task.HasIndex(row => row.Material);
        });
    }
}
