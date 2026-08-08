using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Identity.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class AccessApprovalRequestConfiguration : IEntityTypeConfiguration<AccessApprovalRequest>
{
    public void Configure(EntityTypeBuilder<AccessApprovalRequest> builder)
    {
        builder.ToTable("AccessApprovalRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PayloadHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.DecisionNote)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.RequestedByUserId, x.Action, x.PayloadHash, x.Status });
        builder.HasIndex(x => x.TargetUserId);
    }
}
