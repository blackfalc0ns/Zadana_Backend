using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderComplaintAttachmentConfiguration : IEntityTypeConfiguration<OrderComplaintAttachment>
{
    public void Configure(EntityTypeBuilder<OrderComplaintAttachment> builder)
    {
        builder.ToTable("OrderComplaintAttachments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FileUrl)
            .HasMaxLength(2000)
            .IsRequired();
    }
}
