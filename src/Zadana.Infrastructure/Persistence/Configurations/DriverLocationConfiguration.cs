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

        // Composite index that supports the common access pattern:
        //   "give me the latest location for driver X" — order by RecordedAt
        // descending so SQL Server can serve the query as an index seek + scan
        // backward instead of a sort over the full driver partition.
        builder.HasIndex(x => new { x.DriverId, x.RecordedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_DriverLocations_DriverId_RecordedAt_Desc");
    }
}
