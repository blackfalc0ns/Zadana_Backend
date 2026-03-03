using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendor");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.BusinessNameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(v => v.BusinessNameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(v => v.BusinessType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.CommercialRegistrationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.TaxId)
            .HasMaxLength(50);

        builder.Property(v => v.ContactEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(v => v.ContactPhone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(v => v.CommissionRate)
            .HasPrecision(5, 2);

        builder.Property(v => v.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>()
            .HasDefaultValue(VendorStatus.PendingReview);

        builder.Property(v => v.RejectionReason)
            .HasMaxLength(500);

        builder.Property(v => v.ApprovedAtUtc);
        builder.Property(v => v.ApprovedBy);

        // Relationships
        builder.HasOne(v => v.User)
            .WithOne()
            .HasForeignKey<Vendor>(v => v.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(v => v.UserId)
            .IsUnique()
            .HasDatabaseName("IX_Vendor_UserId");

        builder.HasIndex(v => v.CommercialRegistrationNumber)
            .IsUnique()
            .HasDatabaseName("IX_Vendor_CommRegNum");

        builder.HasIndex(v => v.Status)
            .HasDatabaseName("IX_Vendor_Status");
    }
}
