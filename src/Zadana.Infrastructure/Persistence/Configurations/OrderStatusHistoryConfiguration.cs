using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.OrderId, x.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_OrderStatusHistories_OrderId_CreatedAt_Desc");

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
