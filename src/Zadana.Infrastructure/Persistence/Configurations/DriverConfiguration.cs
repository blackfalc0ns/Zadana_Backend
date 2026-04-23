using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VehicleType)
            .HasConversion(
                vehicleType => DriverVehicleTypeMapper.ToStorageValue(vehicleType),
                value => DriverVehicleTypeMapper.ParseOrNull(value))
            .HasMaxLength(100);
        builder.Property(x => x.NationalId).HasMaxLength(100);
        builder.Property(x => x.LicenseNumber).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.NationalIdImageUrl).HasMaxLength(500);
        builder.Property(x => x.LicenseImageUrl).HasMaxLength(500);
        builder.Property(x => x.VehicleImageUrl).HasMaxLength(500);
        builder.Property(x => x.PersonalPhotoUrl).HasMaxLength(500);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(x => x.ReviewNote).HasMaxLength(500);
        builder.Property(x => x.SuspensionReason).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PrimaryZone)
            .WithMany()
            .HasForeignKey(x => x.PrimaryZoneId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Locations)
            .WithOne(x => x.Driver)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.Driver)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Notes)
            .WithOne(x => x.Driver)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Incidents)
            .WithOne(x => x.Driver)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
