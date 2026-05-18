using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Payments.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PaymentProviderEventInboxConfiguration : IEntityTypeConfiguration<PaymentProviderEventInbox>
{
    public void Configure(EntityTypeBuilder<PaymentProviderEventInbox> builder)
    {
        builder.ToTable("PaymentProviderEventInbox");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderName).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProviderPaymentId).HasMaxLength(200);

        builder.Property(x => x.RawPayload).IsRequired();
        builder.Property(x => x.Headers);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => new { x.ProviderName, x.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("IX_PaymentProviderEventInbox_Provider_EventId");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_PaymentProviderEventInbox_Status");

        builder.HasIndex(x => new { x.ProviderName, x.ProviderPaymentId })
            .HasDatabaseName("IX_PaymentProviderEventInbox_Provider_PaymentId");
    }
}
