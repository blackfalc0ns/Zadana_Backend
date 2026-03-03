using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.LineTotal).HasPrecision(18, 2).IsRequired();

        builder.HasOne(x => x.VendorProduct)
            .WithMany()
            .HasForeignKey(x => x.VendorProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CartId, x.VendorProductId }).IsUnique();
    }
}
