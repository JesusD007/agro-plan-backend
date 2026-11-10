using AgroPlan.Domain.Logbook;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class LogbookAttachmentConfiguration : IEntityTypeConfiguration<LogbookAttachment>
{
    public void Configure(EntityTypeBuilder<LogbookAttachment> builder)
    {
        builder.ToTable("logbook_attachments", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.UploadObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ObjectKey).HasMaxLength(512);
        builder.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => x.UploadObjectKey).IsUnique();
        builder.HasIndex(x => x.ObjectKey).IsUnique();
        builder.HasIndex(x => new { x.FarmId, x.LogbookEntryId });
        builder.HasOne<LogbookEntry>().WithMany().HasForeignKey(x => new { x.FarmId, x.LogbookEntryId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
