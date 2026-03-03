using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.TransferReference).HasMaxLength(200);

        builder.HasOne(x => x.VendorBankAccount)
            .WithMany()
            .HasForeignKey(x => x.VendorBankAccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
