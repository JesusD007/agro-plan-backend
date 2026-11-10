using AgroPlan.Domain.Calendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class CropCalendarConfiguration : IEntityTypeConfiguration<CropCalendarTemplate>
{
    public void Configure(EntityTypeBuilder<CropCalendarTemplate> builder)
    {
        builder.ToTable("crop_calendar_templates", "agro", table =>
        {
            table.HasCheckConstraint("CK_crop_calendar_template_validity",
                "\"EffectiveUntil\" IS NULL OR \"EffectiveUntil\" >= \"EffectiveFrom\"");
            table.HasCheckConstraint("CK_crop_calendar_template_harvest_window",
                "(\"HarvestWindowStartDays\" IS NULL AND \"HarvestWindowEndDays\" IS NULL) OR (\"HarvestWindowStartDays\" >= 0 AND \"HarvestWindowEndDays\" >= \"HarvestWindowStartDays\")");
            table.HasCheckConstraint("CK_crop_calendar_template_validated_provenance",
                "\"Status\" <> 'Validated' OR (\"Source\" <> '' AND \"SourceVersion\" <> '' AND \"TasksJson\" IS NOT NULL)");
        });
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Version).HasMaxLength(80).IsRequired();
        builder.Property(template => template.Source).HasMaxLength(500).IsRequired();
        builder.Property(template => template.SourceVersion).HasMaxLength(200).IsRequired();
        builder.Property(template => template.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(template => template.Region).HasMaxLength(160);
        builder.Property(template => template.CultivationMethod).HasMaxLength(120);
        builder.Property(template => template.TasksJson).HasColumnType("jsonb").IsRequired();
        builder.Ignore(template => template.Tasks);
        builder.HasIndex(template => new { template.CropCatalogEntryId, template.Status, template.EffectiveFrom, template.EffectiveUntil });
        builder.HasOne<AgroPlan.Domain.Crops.CropCatalogEntry>().WithMany()
            .HasForeignKey(template => template.CropCatalogEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
