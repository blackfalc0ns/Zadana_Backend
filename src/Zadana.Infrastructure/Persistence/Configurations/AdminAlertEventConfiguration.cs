using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Social.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public sealed class AdminAlertEventConfiguration : IEntityTypeConfiguration<AdminAlertEvent>
{
    public void Configure(EntityTypeBuilder<AdminAlertEvent> builder)
    {
        builder.ToTable("AdminAlertEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BodyAr).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.BodyEn).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DataJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.DedupeKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.DedupeKey, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.Type, x.ReferenceId, x.CreatedAtUtc });
    }
}

