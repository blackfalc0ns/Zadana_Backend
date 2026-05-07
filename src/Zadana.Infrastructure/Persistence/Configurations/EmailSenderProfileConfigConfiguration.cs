using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Marketing.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class EmailSenderProfileConfigConfiguration : IEntityTypeConfiguration<EmailSenderProfileConfig>
{
    public void Configure(EntityTypeBuilder<EmailSenderProfileConfig> builder)
    {
        builder.ToTable("EmailSenderProfileConfigs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProfileKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReplyTo).HasMaxLength(256).IsRequired();
        builder.Property(x => x.DescriptionKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.ProfileKey)
            .IsUnique()
            .HasDatabaseName("IX_EmailSenderProfileConfigs_ProfileKey");
    }
}
