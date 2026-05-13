using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Social.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class AdminAlertDispatchConfiguration : IEntityTypeConfiguration<AdminAlertDispatch>
{
    public void Configure(EntityTypeBuilder<AdminAlertDispatch> builder)
    {
        builder.ToTable("AdminAlertDispatches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.LastError).HasMaxLength(1000);

        builder.HasIndex(x => new { x.AdminAlertEventId, x.AdminUserId }).IsUnique();
        builder.HasIndex(x => new { x.AdminUserId, x.CreatedAtUtc });
        builder.HasIndex(x => x.NotificationId);

        builder.HasOne(x => x.Event)
            .WithMany(x => x.Dispatches)
            .HasForeignKey(x => x.AdminAlertEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

