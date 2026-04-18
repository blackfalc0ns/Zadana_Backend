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
