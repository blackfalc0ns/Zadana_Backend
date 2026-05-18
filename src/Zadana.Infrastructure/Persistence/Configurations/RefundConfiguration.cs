using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(300);
        builder.Property(x => x.RefundMethod).HasMaxLength(50);
        builder.Property(x => x.CostBearer).HasMaxLength(50);
        
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        // Revised SAR-only refund fields.
        builder.Property(x => x.ProviderName).HasMaxLength(40);
        builder.Property(x => x.ProviderRefundId).HasMaxLength(200);
        builder.Property(x => x.RequestedAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ApprovedAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
        builder.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(30).IsRequired().HasDefaultValue(RefundStatus.Requested);
        builder.Property(x => x.CompensationMethod).HasConversion<string>().HasMaxLength(30).IsRequired().HasDefaultValue(RefundCompensationMethod.SameMethod);
        builder.Property(x => x.RawProviderResponse);

        builder.HasIndex(x => new { x.ProviderName, x.ProviderRefundId })
            .HasFilter("[ProviderRefundId] IS NOT NULL")
            .HasDatabaseName("IX_Refunds_Provider_RefundId");
    }
}
