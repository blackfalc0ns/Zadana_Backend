using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> builder)
    {
        builder.ToTable("DeliveryZones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CenterLat).HasPrecision(10, 7);
        builder.Property(x => x.CenterLng).HasPrecision(10, 7);
        builder.Property(x => x.RadiusKm).HasPrecision(8, 2);
    }
}
