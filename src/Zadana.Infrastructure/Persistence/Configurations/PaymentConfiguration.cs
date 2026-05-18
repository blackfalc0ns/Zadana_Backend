using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Payments.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(x => x.ProviderName).HasMaxLength(100);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        builder.Property(x => x.CheckoutDeviceId).HasMaxLength(200);

        builder.Property(x => x.ProviderMethod).HasMaxLength(40);
        builder.Property(x => x.ProviderInvoiceId).HasMaxLength(200);
        builder.Property(x => x.ProviderStatus).HasMaxLength(40);
        builder.Property(x => x.ProviderReferenceNumber).HasMaxLength(120);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
        builder.Property(x => x.RawCreateResponse);
        builder.Property(x => x.RawFetchResponse);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("IX_Payments_IdempotencyKey");

        builder.HasIndex(x => new { x.ProviderName, x.ProviderTransactionId })
            .HasFilter("[ProviderTransactionId] IS NOT NULL")
            .HasDatabaseName("IX_Payments_Provider_Transaction");

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Refunds)
            .WithOne(x => x.Payment)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
