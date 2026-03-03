using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DeliveryProofConfiguration : IEntityTypeConfiguration<DeliveryProof>
{
    public void Configure(EntityTypeBuilder<DeliveryProof> builder)
    {
        builder.ToTable("DeliveryProofs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProofType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.OtpCode).HasMaxLength(50);
        builder.Property(x => x.RecipientName).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(300);
    }
}
