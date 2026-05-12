using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class AccessAuditLogConfiguration : IEntityTypeConfiguration<AccessAuditLog>
{
    public void Configure(EntityTypeBuilder<AccessAuditLog> builder)
    {
        builder.ToTable("AccessAuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.BeforeJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.AfterJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TargetUserId, x.CreatedAtUtc });
        builder.HasIndex(x => x.ActorUserId);
    }
}
