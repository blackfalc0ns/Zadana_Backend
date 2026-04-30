using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderSupportCaseAttachmentConfiguration : IEntityTypeConfiguration<OrderSupportCaseAttachment>
{
    public void Configure(EntityTypeBuilder<OrderSupportCaseAttachment> builder)
    {
        builder.ToTable("OrderSupportCaseAttachments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileUrl).HasMaxLength(2000).IsRequired();
    }
}
