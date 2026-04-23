using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Delivery.Entities;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class DriverNoteConfiguration : IEntityTypeConfiguration<DriverNote>
{
    public void Configure(EntityTypeBuilder<DriverNote> builder)
    {
        builder.ToTable("DriverNotes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();

        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
