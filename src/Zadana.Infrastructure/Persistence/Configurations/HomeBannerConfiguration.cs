using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class HomeBannerConfiguration : IEntityTypeConfiguration<HomeBanner>
{
    public void Configure(EntityTypeBuilder<HomeBanner> builder)
    {
        builder.ToTable("HomeBanner");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TagAr)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TagEn)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TitleAr)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.TitleEn)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SubtitleAr)
            .HasMaxLength(500);

        builder.Property(x => x.SubtitleEn)
            .HasMaxLength(500);

        builder.Property(x => x.ActionLabelAr)
            .HasMaxLength(100);

        builder.Property(x => x.ActionLabelEn)
            .HasMaxLength(100);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.IsActive, x.DisplayOrder })
            .HasDatabaseName("IX_HomeBanner_IsActive_DisplayOrder");
    }
}
