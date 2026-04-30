using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderSupportCaseConfiguration : IEntityTypeConfiguration<OrderSupportCase>
{
    public void Configure(EntityTypeBuilder<OrderSupportCase> builder)
    {
        builder.ToTable("OrderSupportCases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Queue).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100);
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DecisionNotes).HasMaxLength(2000);
        builder.Property(x => x.CustomerVisibleNote).HasMaxLength(2000);
        builder.Property(x => x.RequestedRefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.ApprovedRefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.RefundMethod).HasMaxLength(50);
        builder.Property(x => x.CostBearer).HasMaxLength(50);

        builder.HasIndex(x => new { x.OrderId, x.Status });

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.OrderSupportCase)
            .HasForeignKey(x => x.OrderSupportCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Activities)
            .WithOne(x => x.OrderSupportCase)
            .HasForeignKey(x => x.OrderSupportCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
