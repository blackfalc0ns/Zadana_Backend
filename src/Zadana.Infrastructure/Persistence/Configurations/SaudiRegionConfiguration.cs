using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Geography.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class SaudiRegionConfiguration : IEntityTypeConfiguration<SaudiRegion>
{
    public void Configure(EntityTypeBuilder<SaudiRegion> builder)
    {
        builder.ToTable("SaudiRegions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.Property(r => r.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(r => r.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Latitude).IsRequired();
        builder.Property(r => r.Longitude).IsRequired();
        builder.Property(r => r.MapZoom).IsRequired();
        builder.Property(r => r.SortOrder).IsRequired();

        builder.HasIndex(r => r.Code).IsUnique();

        builder.HasMany(r => r.Cities)
               .WithOne(c => c.Region)
               .HasForeignKey(c => c.RegionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
