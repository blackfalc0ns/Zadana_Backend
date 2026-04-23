using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverLocationConfiguration : IEntityTypeConfiguration<DriverLocation>
{
    public void Configure(EntityTypeBuilder<DriverLocation> builder)
    {
        builder.ToTable("DriverLocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Latitude).HasPrecision(10, 7).IsRequired();
        builder.Property(x => x.Longitude).HasPrecision(10, 7).IsRequired();
        builder.Property(x => x.AccuracyMeters).HasPrecision(8, 2);
    }
}
