using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.UnitName).HasMaxLength(100);

        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.LineDiscount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.LineTotal).HasPrecision(18, 2).IsRequired();

        builder.HasOne(x => x.VendorProduct)
            .WithMany()
            .HasForeignKey(x => x.VendorProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MasterProduct)
            .WithMany()
            .HasForeignKey(x => x.MasterProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
