using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class SystemLogEntryConfiguration : IEntityTypeConfiguration<SystemLogEntry>
{
    public void Configure(EntityTypeBuilder<SystemLogEntry> builder)
    {
        builder.ToTable("SystemLogEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceApp)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Module)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.RequestPath)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(x => x.HttpMethod)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.ActorFullName)
            .HasMaxLength(200);

        builder.Property(x => x.ActorEmail)
            .HasMaxLength(256);

        builder.Property(x => x.ActorRole)
            .HasMaxLength(100);

        builder.Property(x => x.TargetEntityType)
            .HasMaxLength(100);

        builder.Property(x => x.TargetEntityId)
            .HasMaxLength(100);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.Property(x => x.QueryString)
            .HasMaxLength(1000);

        builder.Property(x => x.RequestPayloadJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.MetadataJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.SourceApp, x.Module, x.OccurredAtUtc });
        builder.HasIndex(x => x.ActorUserId);
        builder.HasIndex(x => new { x.TargetEntityType, x.TargetEntityId });
        builder.HasIndex(x => x.IsSuccess);
    }
}
