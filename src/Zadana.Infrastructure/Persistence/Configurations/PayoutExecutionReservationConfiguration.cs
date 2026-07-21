using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class PayoutExecutionReservationConfiguration : IEntityTypeConfiguration<PayoutExecutionReservation>
{
    public void Configure(EntityTypeBuilder<PayoutExecutionReservation> builder)
    {
        builder.ToTable("PayoutExecutionReservations");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Mode)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(item => item.SubmissionReference)
            .HasMaxLength(200);

        builder.Property(item => item.ReleaseReason)
            .HasMaxLength(1000);

        builder.Property(item => item.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(item => item.PayoutId)
            .IsUnique()
            .HasDatabaseName("IX_PayoutExecutionReservations_PayoutId");

        builder.HasIndex(item => new { item.Mode, item.Status, item.ClaimedAtUtc })
            .HasDatabaseName("IX_PayoutExecutionReservations_Mode_Status_ClaimedAt");
    }
}
