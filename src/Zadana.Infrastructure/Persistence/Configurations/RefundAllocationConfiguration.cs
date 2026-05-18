using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.SharedKernel.Finance;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class RefundAllocationConfiguration : IEntityTypeConfiguration<RefundAllocation>
{
    public void Configure(EntityTypeBuilder<RefundAllocation> builder)
    {
        builder.ToTable("RefundAllocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProductAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DeliveryAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.VatAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.CodFeeAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PlatformAbsorbedAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.VendorRecoveryAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DriverRecoveryAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue(CurrencyPolicy.OfficialCurrency);

        builder.HasOne(x => x.Refund)
            .WithMany()
            .HasForeignKey(x => x.RefundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RefundId)
            .IsUnique()
            .HasDatabaseName("IX_RefundAllocations_RefundId");
    }
}
