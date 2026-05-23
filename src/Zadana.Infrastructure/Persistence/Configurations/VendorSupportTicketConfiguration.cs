using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class VendorSupportTicketConfiguration : IEntityTypeConfiguration<VendorSupportTicket>
{
    public void Configure(EntityTypeBuilder<VendorSupportTicket> builder)
    {
        builder.ToTable("VendorSupportTickets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reference).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.LastMessagePreview).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.VendorId, x.UpdatedAtUtc });
        builder.HasIndex(x => new { x.VendorId, x.Status, x.UpdatedAtUtc });
        builder.HasIndex(x => x.OrderId);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.VendorSupportTicket)
            .HasForeignKey(x => x.VendorSupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
