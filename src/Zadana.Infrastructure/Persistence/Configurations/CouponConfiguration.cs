using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        
        builder.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(50).IsRequired();
        
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinOrderAmount).HasPrecision(18, 2);
        builder.Property(x => x.MaxDiscountAmount).HasPrecision(18, 2);

        builder.HasMany(x => x.ApplicableVendors)
            .WithOne(x => x.Coupon)
            .HasForeignKey(x => x.CouponId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
