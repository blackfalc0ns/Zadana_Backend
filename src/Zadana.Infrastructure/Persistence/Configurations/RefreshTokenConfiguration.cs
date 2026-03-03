using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(r => r.ExpiresAtUtc)
            .IsRequired();

        builder.Property(r => r.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.RevokedAtUtc);

        // Relationships
        builder.HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_RefreshToken_UserId");

        builder.HasIndex(r => r.Token)
            .HasDatabaseName("IX_RefreshToken_Token");
    }
}
