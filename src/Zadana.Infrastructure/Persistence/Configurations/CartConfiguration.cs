using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DiscountTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DeliveryFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.GuestId).HasMaxLength(200);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Cart)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(x => x.GuestId)
            .IsUnique()
            .HasFilter("[GuestId] IS NOT NULL");
    }
}
