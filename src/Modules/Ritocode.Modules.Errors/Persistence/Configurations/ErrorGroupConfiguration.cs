using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Errors.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Errors.Persistence.Configurations;

internal sealed class ErrorGroupConfiguration : IEntityTypeConfiguration<ErrorGroup>
{
    public const string OnePerFingerprintIndex = "ux_error_groups_fingerprint";

    public void Configure(EntityTypeBuilder<ErrorGroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("error_groups", table => table.HasEnumCheckConstraint<ErrorGroup, ErrorSource>("source"));

        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();

        builder.Property(group => group.Fingerprint).HasMaxLength(ErrorGroup.FingerprintLength).IsFixedLength().IsRequired();
        builder.Property(group => group.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(group => group.Type).HasMaxLength(ErrorGroup.TypeMaxLength).IsRequired();
        builder.Property(group => group.Message).HasMaxLength(ErrorGroup.MessageMaxLength).IsRequired();
        builder.Property(group => group.Location).HasMaxLength(ErrorGroup.LocationMaxLength).IsRequired();
        builder.Property(group => group.Count).IsRequired();
        builder.Property(group => group.FirstSeenAt).IsRequired();
        builder.Property(group => group.LastSeenAt).IsRequired();

        // One group per fingerprint: the first two occurrences racing each other make one group, not two.
        builder.HasIndex(group => group.Fingerprint).HasDatabaseName(OnePerFingerprintIndex).IsUnique();

        // The admin list: open or resolved, the latest first.
        builder.HasIndex(group => new { group.ResolvedAt, group.LastSeenAt });
    }
}
