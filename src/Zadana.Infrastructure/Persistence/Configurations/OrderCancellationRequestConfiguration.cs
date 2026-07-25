using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class OrderCancellationRequestConfiguration : IEntityTypeConfiguration<OrderCancellationRequest>
{
    public void Configure(EntityTypeBuilder<OrderCancellationRequest> builder)
    {
        builder.ToTable("OrderCancellationRequests");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.CustomerReason).HasMaxLength(1000);
        builder.Property(item => item.VendorResponseNote).HasMaxLength(1000);

        builder.HasIndex(item => new { item.OrderId, item.Status })
            .HasDatabaseName("IX_OrderCancellationRequests_OrderId_Status");

        builder.HasOne(item => item.Order)
            .WithMany(order => order.CancellationRequests)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
