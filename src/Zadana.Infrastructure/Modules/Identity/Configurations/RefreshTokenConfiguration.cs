using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Modules.Identity.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.ExpiresAtUtc)
            .IsRequired();

        builder.Property(r => r.IsRevoked)
            .IsRequired();

        // The HasOne / WithMany is already defined in UserConfiguration, 
        // but can be safely skipped or reiterated here.

        builder.HasIndex(r => r.Token)
            .IsUnique();
    }
}
