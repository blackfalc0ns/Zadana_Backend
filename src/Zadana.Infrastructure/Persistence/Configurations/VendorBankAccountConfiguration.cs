using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class VendorBankAccountConfiguration : IEntityTypeConfiguration<VendorBankAccount>
{
    public void Configure(EntityTypeBuilder<VendorBankAccount> builder)
    {
        builder.ToTable("VendorBankAccount");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.BankName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.AccountHolderName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.IBAN)
            .IsRequired()
            .HasMaxLength(34);

        builder.Property(a => a.SwiftCode)
            .HasMaxLength(11);

        builder.Property(a => a.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>()
            .HasDefaultValue(BankAccountStatus.PendingVerification);

        builder.Property(a => a.RejectionReason)
            .HasMaxLength(500);

        builder.Property(a => a.VerifiedAtUtc);
        builder.Property(a => a.VerifiedBy);

        // Relationships
        builder.HasOne(a => a.Vendor)
            .WithMany(v => v.BankAccounts)
            .HasForeignKey(a => a.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(a => a.VendorId)
            .HasDatabaseName("IX_VendorBankAccount_VendorId");
    }
}
