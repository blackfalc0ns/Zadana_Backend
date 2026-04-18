using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Social.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BodyAr).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.BodyEn).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.Data).HasMaxLength(4000);

        // Ignore computed properties (legacy compatibility)
        builder.Ignore(x => x.Title);
        builder.Ignore(x => x.Body);

        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.UserId, x.Type, x.CreatedAtUtc });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

