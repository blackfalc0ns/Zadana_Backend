using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Finances.Entities;
using Zadana.SharedKernel.Finance;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PaymentGatewaySettlementConfiguration : IEntityTypeConfiguration<PaymentGatewaySettlement>
{
    public void Configure(EntityTypeBuilder<PaymentGatewaySettlement> builder)
    {
        builder.ToTable("PaymentGatewaySettlements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderName).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProviderSettlementId).HasMaxLength(200).IsRequired();

        builder.Property(x => x.SettlementDate).IsRequired();

        builder.Property(x => x.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.FeeAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NetAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue(CurrencyPolicy.OfficialCurrency);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.RawFileOrJson);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.ProviderName, x.ProviderSettlementId })
            .IsUnique()
            .HasDatabaseName("IX_PaymentGatewaySettlements_Provider_Id");

        builder.Metadata.FindNavigation(nameof(PaymentGatewaySettlement.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
