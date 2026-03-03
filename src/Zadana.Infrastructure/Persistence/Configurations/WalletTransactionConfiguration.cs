using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TxnType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Direction).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasOne(x => x.Wallet)
            .WithMany(w => w.Transactions)
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payment)
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Settlement)
            .WithMany()
            .HasForeignKey(x => x.SettlementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
