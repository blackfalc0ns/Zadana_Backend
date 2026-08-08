using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class EmailDispatchLogConfiguration : IEntityTypeConfiguration<EmailDispatchLog>
{
    public void Configure(EntityTypeBuilder<EmailDispatchLog> builder)
    {
        builder.ToTable("EmailDispatchLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleKey).HasMaxLength(100);
        builder.Property(x => x.RuleLabel).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AudienceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(50);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.Property(x => x.EventKey).HasMaxLength(100);

        builder.Property(x => x.ToRecipientsJson).IsRequired();
        builder.Property(x => x.CcRecipientsJson).IsRequired();
        builder.Property(x => x.BccRecipientsJson).IsRequired();

        builder.HasIndex(x => new { x.RuleKey, x.CreatedAtUtc })
            .HasDatabaseName("IX_EmailDispatchLogs_RuleKey_CreatedAtUtc");

        builder.HasIndex(x => new { x.Source, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_EmailDispatchLogs_Source_Status_CreatedAtUtc");
    }
}
