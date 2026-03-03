using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VehicleType).HasMaxLength(100);
        builder.Property(x => x.NationalId).HasMaxLength(100);
        builder.Property(x => x.LicenseNumber).HasMaxLength(100);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Locations)
            .WithOne(x => x.Driver)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.Driver)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
