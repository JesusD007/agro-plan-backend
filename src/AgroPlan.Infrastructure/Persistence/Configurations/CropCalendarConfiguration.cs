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

public sealed class ScheduleChangeProposalConfiguration : IEntityTypeConfiguration<ScheduleChangeProposal>
{
    public void Configure(EntityTypeBuilder<ScheduleChangeProposal> builder)
    {
        builder.ToTable("schedule_change_proposals", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.EvidenceVersion).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DeltasJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.EvidenceIdsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ExplanationJson).HasColumnType("jsonb");
        builder.Property(x => x.ExplanationProvider).HasMaxLength(80);
        builder.Property(x => x.ExplanationModel).HasMaxLength(160);
        builder.Property(x => x.ExplanationPromptVersion).HasMaxLength(80);
        builder.Property(x => x.ExplanationSchemaVersion).HasMaxLength(80);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.ProcessingStartedAt);
        builder.Property(x => x.ProcessingAttempts).IsRequired();
        builder.Ignore(x => x.Deltas);
        builder.Ignore(x => x.EvidenceIds);
        builder.HasIndex(x => new { x.FarmId, x.CropCycleId, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}

public sealed class CalendarScheduleItemConfiguration : IEntityTypeConfiguration<CalendarScheduleItem>
{
    public void Configure(EntityTypeBuilder<CalendarScheduleItem> builder)
    {
        builder.ToTable("calendar_schedule_items", "agro");
        builder.HasKey(x => new { x.FarmId, x.CropCycleId, x.TaskKey });
        builder.Property(x => x.TaskKey).HasMaxLength(120).IsRequired();
        builder.HasOne<AgroPlan.Domain.Crops.CropCycle>().WithMany().HasForeignKey(x => new { x.FarmId, x.CropCycleId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleChangeProposal>().WithMany().HasForeignKey(x => new { x.FarmId, x.LastProposalId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
