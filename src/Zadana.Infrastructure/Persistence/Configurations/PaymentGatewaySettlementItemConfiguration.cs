using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.SharedKernel.Finance;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PaymentGatewaySettlementItemConfiguration : IEntityTypeConfiguration<PaymentGatewaySettlementItem>
{
    public void Configure(EntityTypeBuilder<PaymentGatewaySettlementItem> builder)
    {
        builder.ToTable("PaymentGatewaySettlementItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderPaymentId).HasMaxLength(200).IsRequired();

        builder.Property(x => x.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NetAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue(CurrencyPolicy.OfficialCurrency);

        builder.Property(x => x.MatchStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Metadata);
        builder.Property(x => x.MatchNote).HasMaxLength(500);

        builder.HasOne(x => x.Settlement)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SettlementId, x.ProviderPaymentId })
            .HasDatabaseName("IX_PaymentGatewaySettlementItems_Settlement_PaymentId");

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("IX_PaymentGatewaySettlementItems_OrderId");
    }
}
