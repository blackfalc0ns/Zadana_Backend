using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class VendorSupportTicketMessageConfiguration : IEntityTypeConfiguration<VendorSupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<VendorSupportTicketMessage> builder)
    {
        builder.ToTable("VendorSupportTicketMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AuthorRole).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => new { x.VendorSupportTicketId, x.CreatedAtUtc });
        builder.HasIndex(x => x.AuthorUserId);
    }
}
