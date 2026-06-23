using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Social.Support;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class UserPushDeviceConfiguration : IEntityTypeConfiguration<UserPushDevice>
{
    public void Configure(EntityTypeBuilder<UserPushDevice> builder)
    {
        builder.ToTable("UserPushDevices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceToken)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(x => x.Platform)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasMaxLength(200);

        builder.Property(x => x.DeviceName)
            .HasMaxLength(200);

        builder.Property(x => x.AppVersion)
            .HasMaxLength(50);

        builder.Property(x => x.Locale)
            .HasMaxLength(20);

        builder.Property(x => x.NotificationSound)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(NotificationSoundCatalog.Classic);

        builder.Property(x => x.CategoryNotificationSoundsJson)
            .HasMaxLength(512);

        builder.Property(x => x.DispatchPushEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.AssignmentPushEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.SupportPushEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.WalletPushEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.AccountPushEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.AdminDriversPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminVendorsPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminCatalogPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminDisputesPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminRefundsPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminSettlementsPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminSupportPushEnabled).HasDefaultValue(true);
        builder.Property(x => x.AdminSystemPushEnabled).HasDefaultValue(true);

        builder.HasIndex(x => x.DeviceToken).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasIndex(x => new { x.UserId, x.DeviceId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.PushDevices)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
