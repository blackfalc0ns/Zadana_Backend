using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Geography.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class SaudiCityConfiguration : IEntityTypeConfiguration<SaudiCity>
{
    public void Configure(EntityTypeBuilder<SaudiCity> builder)
    {
        builder.ToTable("SaudiCities");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Latitude).IsRequired();
        builder.Property(c => c.Longitude).IsRequired();
        builder.Property(c => c.MapZoom).IsRequired();
        builder.Property(c => c.SortOrder).IsRequired();

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.RegionId);
    }
}
