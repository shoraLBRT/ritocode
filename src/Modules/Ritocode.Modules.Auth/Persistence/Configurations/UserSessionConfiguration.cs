using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ritocode.Modules.Auth.Domain;

namespace Ritocode.Modules.Auth.Persistence.Configurations;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();

        builder.Property(session => session.UserId).IsRequired();
        builder.Property(session => session.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(session => session.CsrfToken).HasMaxLength(64).IsRequired();
        builder.Property(session => session.CreatedAt).IsRequired();
        builder.Property(session => session.ExpiresAt).IsRequired();

        // Every authenticated request finds its session by the token's hash.
        builder.HasIndex(session => session.TokenHash).IsUnique();

        // A user's sessions, for signing out everywhere and for the admin area.
        builder.HasIndex(session => session.UserId);
    }
}
