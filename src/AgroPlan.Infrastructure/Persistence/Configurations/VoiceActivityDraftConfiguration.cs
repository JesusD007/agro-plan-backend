using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Voice;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class VoiceActivityDraftConfiguration : IEntityTypeConfiguration<VoiceActivityDraft>
{
    public void Configure(EntityTypeBuilder<VoiceActivityDraft> builder)
    {
        builder.ToTable("voice_activity_drafts", "agro");
        builder.HasKey(draft => draft.Id);
        builder.HasAlternateKey(draft => new { draft.FarmId, draft.Id });
        builder.Property(draft => draft.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(draft => draft.Language).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(draft => draft.ActivityType).HasMaxLength(24);
        builder.Property(draft => draft.SubmittedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(draft => draft.ConsentActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(draft => draft.ConsentNoticeVersion).HasMaxLength(80).IsRequired();
        builder.Property(draft => draft.UploadObjectKey).HasMaxLength(1024).IsRequired();
        builder.Property(draft => draft.AudioObjectKey).HasMaxLength(1024);
        builder.Property(draft => draft.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(draft => draft.FailureCode).HasMaxLength(80);
        builder.Property(draft => draft.AudioDeleteLastError).HasMaxLength(80);
        builder.Property(draft => draft.UncertainFieldsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(draft => draft.Version).IsConcurrencyToken();
        builder.HasIndex(draft => new { draft.FarmId, draft.Status, draft.CreatedAt });
        builder.HasIndex(draft => new { draft.Status, draft.RetryAt });
        builder.HasIndex(draft => new { draft.AudioDeleteAfter, draft.AudioDeleteRetryAt, draft.AudioDeletedAt });
        builder.HasIndex(draft => draft.AudioObjectKey).IsUnique().HasFilter("\"AudioObjectKey\" IS NOT NULL");
        builder.HasIndex(draft => draft.UploadObjectKey).IsUnique();
        builder.HasOne<Farm>().WithMany().HasForeignKey(draft => draft.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(draft => new { draft.FarmId, UserId = draft.SubmittedByUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(draft => new { draft.FarmId, draft.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Parcel>().WithMany()
            .HasForeignKey(draft => new { draft.FarmId, draft.ParcelId })
            .HasPrincipalKey(parcel => new { parcel.FarmId, parcel.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany()
            .HasForeignKey(draft => new { draft.FarmId, draft.CropCycleId })
            .HasPrincipalKey(cycle => new { cycle.FarmId, cycle.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LogbookEntry>().WithMany()
            .HasForeignKey(draft => new { draft.FarmId, draft.LogbookEntryId })
            .HasPrincipalKey(entry => new { entry.FarmId, entry.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
