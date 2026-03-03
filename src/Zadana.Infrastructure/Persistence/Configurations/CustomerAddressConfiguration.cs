using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.ContactName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactPhone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AddressLine).HasMaxLength(500).IsRequired();
        builder.Property(x => x.BuildingNo).HasMaxLength(50);
        builder.Property(x => x.FloorNo).HasMaxLength(50);
        builder.Property(x => x.ApartmentNo).HasMaxLength(50);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Area).HasMaxLength(100);
        
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
