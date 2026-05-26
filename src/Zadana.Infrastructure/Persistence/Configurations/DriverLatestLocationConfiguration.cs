using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverLatestLocationConfiguration : IEntityTypeConfiguration<DriverLatestLocation>
{
    public void Configure(EntityTypeBuilder<DriverLatestLocation> builder)
    {
        builder.ToTable("DriverLatestLocations");

        // Primary key is the DriverId itself — single row per driver.
        builder.HasKey(x => x.DriverId);

        builder.Property(x => x.Latitude).HasPrecision(10, 7).IsRequired();
        builder.Property(x => x.Longitude).HasPrecision(10, 7).IsRequired();
        builder.Property(x => x.AccuracyMeters).HasPrecision(8, 2);
        builder.Property(x => x.RecordedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasOne(x => x.Driver)
            .WithOne()
            .HasForeignKey<DriverLatestLocation>(x => x.DriverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
