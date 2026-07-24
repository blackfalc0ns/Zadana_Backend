using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PlatformContactSettingsConfiguration : IEntityTypeConfiguration<PlatformContactSettings>
{
    public void Configure(EntityTypeBuilder<PlatformContactSettings> builder)
    {
        builder.ToTable("PlatformContactSettings");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.SupportEmail).HasMaxLength(256);
        builder.Property(item => item.SupportPhone).HasMaxLength(32);
        builder.Property(item => item.WhatsAppUrl).HasMaxLength(500);
        builder.Property(item => item.InstagramUrl).HasMaxLength(500);
        builder.Property(item => item.TwitterUrl).HasMaxLength(500);
        builder.Property(item => item.TikTokUrl).HasMaxLength(500);
        builder.Property(item => item.SnapchatUrl).HasMaxLength(500);
        builder.Property(item => item.FacebookUrl).HasMaxLength(500);
        builder.Property(item => item.YouTubeUrl).HasMaxLength(500);
        builder.Property(item => item.LinkedInUrl).HasMaxLength(500);
    }
}
