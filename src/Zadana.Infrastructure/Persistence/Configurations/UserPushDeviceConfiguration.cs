using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

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

        builder.HasIndex(x => x.DeviceToken).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasIndex(x => new { x.UserId, x.DeviceId });

        builder.HasOne(x => x.User)
            .WithMany(x => x.PushDevices)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
