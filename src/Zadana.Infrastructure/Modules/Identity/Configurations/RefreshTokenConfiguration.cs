using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Modules.Identity.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // The legacy schema uses table name "RefreshToken" (singular) and an
        // index "IX_RefreshToken_Token" with Token max length 512. We don't
        // override the table name here; EF picks up the established name from
        // the existing snapshot/migrations. Only column-level changes are
        // declared so we don't accidentally rename a live table.

        builder.HasKey(r => r.Id);

        // Legacy plaintext column. Kept nullable so existing rows continue to
        // round-trip; new rows leave it null.
        builder.Property(r => r.Token)
            .HasMaxLength(512);

        // New hashed column. SHA-256 hex digest = 64 chars; we leave room
        // (128) for future algorithm upgrades.
        builder.Property(r => r.TokenHash)
            .HasMaxLength(128);

        builder.Property(r => r.ExpiresAtUtc)
            .IsRequired();

        builder.Property(r => r.IsRevoked)
            .IsRequired();

        builder.Property(r => r.WasReused)
            .HasDefaultValue(false);

        builder.HasIndex(r => r.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshToken_Token")
            .HasFilter("[Token] IS NOT NULL");

        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_RefreshToken_TokenHash")
            .HasFilter("[TokenHash] IS NOT NULL");
    }
}
