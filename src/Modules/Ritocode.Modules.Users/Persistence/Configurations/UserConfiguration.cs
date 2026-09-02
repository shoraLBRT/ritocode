using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Users.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Users.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("users", table => table.HasEnumCheckConstraint<User, TrustLevel>("trust_level"));

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.Username).HasMaxLength(User.UsernameMaxLength).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.Xp).IsRequired().HasDefaultValue(0);

        // Text rather than an integer so the column is legible in psql; the check constraint
        // above stops anything outside the enum being written.
        builder.Property(u => u.TrustLevel)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Both values are stored already lower-cased, so a plain unique index is
        // case-insensitive in effect without needing citext or a functional index.
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint("ck_users_xp_not_negative", "xp >= 0"));
    }
}
