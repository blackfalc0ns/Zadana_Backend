using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderSupportCaseActivityConfiguration : IEntityTypeConfiguration<OrderSupportCaseActivity>
{
    public void Configure(EntityTypeBuilder<OrderSupportCaseActivity> builder)
    {
        builder.ToTable("OrderSupportCaseActivities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.ActorRole).HasMaxLength(50).IsRequired();
    }
}
