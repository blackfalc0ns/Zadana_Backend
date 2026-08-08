using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class EmailWorkflowRuleConfigConfiguration : IEntityTypeConfiguration<EmailWorkflowRuleConfig>
{
    public void Configure(EntityTypeBuilder<EmailWorkflowRuleConfig> builder)
    {
        builder.ToTable("EmailWorkflowRuleConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TitleKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubtitleKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CategoryKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.CadenceLabelKey).HasMaxLength(150).IsRequired();
        builder.Property(x => x.TriggerNotesKey).HasMaxLength(400).IsRequired();
        builder.Property(x => x.SenderProfileKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AudienceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PanelScope).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BranchScopeMode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AutomationState).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EventKey).HasMaxLength(100);

        builder.Property(x => x.PersonaTargetsJson).IsRequired();
        builder.Property(x => x.EntityScopeJson).IsRequired();
        builder.Property(x => x.RecipientTargetsJson).IsRequired();
        builder.Property(x => x.RouteJson).IsRequired();
        builder.Property(x => x.TemplateJson).IsRequired();

        builder.HasIndex(x => x.RuleKey)
            .IsUnique()
            .HasDatabaseName("IX_EmailWorkflowRuleConfigs_RuleKey");

        builder.HasIndex(x => x.EventKey)
            .HasDatabaseName("IX_EmailWorkflowRuleConfigs_EventKey");
    }
}
