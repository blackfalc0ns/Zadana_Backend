using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CouponVendorConfiguration : IEntityTypeConfiguration<CouponVendor>
{
    public void Configure(EntityTypeBuilder<CouponVendor> builder)
    {
        builder.ToTable("CouponVendors");

        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.CouponId, x.VendorId }).IsUnique();
    }
}
