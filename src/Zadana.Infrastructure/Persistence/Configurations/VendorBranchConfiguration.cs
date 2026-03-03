using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorBranchConfiguration : IEntityTypeConfiguration<VendorBranch>
{
    public void Configure(EntityTypeBuilder<VendorBranch> builder)
    {
        builder.ToTable("VendorBranch");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.AddressLine)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Latitude)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(b => b.Longitude)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(b => b.ContactPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.DeliveryRadiusKm)
            .IsRequired()
            .HasPrecision(5, 2);

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Relationships
        builder.HasOne(b => b.Vendor)
            .WithMany(v => v.Branches)
            .HasForeignKey(b => b.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(b => b.VendorId)
            .HasDatabaseName("IX_VendorBranch_VendorId");
    }
}
