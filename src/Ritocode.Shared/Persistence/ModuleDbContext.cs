using Microsoft.EntityFrameworkCore;

namespace Ritocode.Shared.Persistence;

/// <summary>
/// Base for a module's <see cref="DbContext"/>. Each module owns one PostgreSQL schema and maps
/// only its own tables, so a module cannot reach another module's data through EF at all — the
/// database-level form of the boundary the architecture tests enforce in code.
/// See <c>docs/adr/0004-persistence-and-migrations.md</c>.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>PostgreSQL schema this module owns. Every table and its migration history live here.</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        // Configurations are applied explicitly by each context rather than discovered by
        // scanning, so the mapped surface of a module is readable in one method.
        base.OnModelCreating(modelBuilder);
    }
}
