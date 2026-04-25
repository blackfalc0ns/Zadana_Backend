using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverPayoutMethodConfiguration : IEntityTypeConfiguration<DriverPayoutMethod>
{
    public void Configure(EntityTypeBuilder<DriverPayoutMethod> builder)
    {
        builder.ToTable("DriverPayoutMethods");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MethodType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(DriverPayoutMethodType.BankAccount)
            .IsRequired();

        builder.Property(x => x.AccountHolderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ProviderName)
            .HasMaxLength(200);

        builder.Property(x => x.AccountIdentifier)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MaskedLabel)
            .HasMaxLength(250)
            .IsRequired();

        builder.HasIndex(x => x.DriverId);
    }
}
