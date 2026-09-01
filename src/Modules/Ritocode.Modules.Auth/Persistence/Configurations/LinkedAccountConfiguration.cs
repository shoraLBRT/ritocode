using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Auth.Domain;
using Ritocode.Shared.Persistence;

namespace Ritocode.Modules.Auth.Persistence.Configurations;

internal sealed class LinkedAccountConfiguration : IEntityTypeConfiguration<LinkedAccount>
{
    public void Configure(EntityTypeBuilder<LinkedAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("linked_accounts",
            table => table.HasEnumCheckConstraint<LinkedAccount, IdentityProvider>("provider"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.ProviderUserId)
            .HasMaxLength(LinkedAccount.ProviderUserIdMaxLength).IsRequired();
        builder.Property(a => a.ProviderLogin)
            .HasMaxLength(LinkedAccount.ProviderLoginMaxLength).IsRequired();
        builder.Property(a => a.LinkedAt).IsRequired();

        // One Ritocode account per external identity: signing in with a GitHub account that is
        // already linked must resolve to the same user rather than creating a second one.
        builder.HasIndex(a => new { a.Provider, a.ProviderUserId }).IsUnique();

        // And at most one identity per provider per user.
        builder.HasIndex(a => new { a.UserId, a.Provider }).IsUnique();
    }
}
