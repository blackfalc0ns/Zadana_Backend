using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class PlatformBankAccountConfiguration : IEntityTypeConfiguration<PlatformBankAccount>
{
    public void Configure(EntityTypeBuilder<PlatformBankAccount> builder)
    {
        builder.ToTable("PlatformBankAccounts");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.BankName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.AccountHolderName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.IBAN)
            .IsRequired()
            .HasMaxLength(34);

        builder.Property(item => item.AccountNumber)
            .HasMaxLength(64);

        builder.Property(item => item.CountryCode)
            .IsRequired()
            .HasMaxLength(2)
            .HasDefaultValue("SA");

        builder.Property(item => item.City)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("Riyadh");

        builder.Property(item => item.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(item => item.IsBankTransferEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(item => item.IsMoyasarPayoutsEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(item => item.MoyasarPayoutSourceId)
            .HasMaxLength(100);

        builder.Property(item => item.Notes)
            .HasMaxLength(500);

        builder.HasIndex(item => item.IsActive)
            .HasDatabaseName("IX_PlatformBankAccounts_IsActive");
    }
}
