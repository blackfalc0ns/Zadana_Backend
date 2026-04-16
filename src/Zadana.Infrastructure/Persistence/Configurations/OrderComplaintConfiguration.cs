using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderComplaintConfiguration : IEntityTypeConfiguration<OrderComplaint>
{
    public void Configure(EntityTypeBuilder<OrderComplaint> builder)
    {
        builder.ToTable("OrderComplaints");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Message)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.OrderId)
            .IsUnique();

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.OrderComplaint)
            .HasForeignKey(x => x.OrderComplaintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
