using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Symptoms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class SymptomReportConfiguration : IEntityTypeConfiguration<SymptomReport>
{
    public void Configure(EntityTypeBuilder<SymptomReport> builder)
    {
        builder.ToTable("symptom_reports", "agro");
        builder.HasKey(report => report.Id);
        builder.HasAlternateKey(report => new { report.FarmId, report.Id });
        builder.Property(report => report.SubmittedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(report => report.Notes).HasMaxLength(2000);
        builder.Property(report => report.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(report => report.VisibleSigns).HasMaxLength(4000);
        builder.Property(report => report.Uncertainty).HasMaxLength(2000);
        builder.Property(report => report.RequestedEvidence).HasColumnType("text[]").IsRequired();
        builder.Property(report => report.HumanDiagnosis).HasMaxLength(2000);
        builder.Property(report => report.HumanTreatment).HasMaxLength(4000);
        builder.Property(report => report.ReviewedByUserId).HasMaxLength(450);
        builder.Property(report => report.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(report => new { report.FarmId, report.Status, report.CreatedAt });
        builder.HasIndex(report => new { report.Status, report.ProcessingStartedAt });
        builder.HasOne<Farm>().WithMany().HasForeignKey(report => report.FarmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(report => new { report.FarmId, UserId = report.SubmittedByUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany()
            .HasForeignKey(report => new { report.FarmId, report.CropCycleId })
            .HasPrincipalKey(cycle => new { cycle.FarmId, cycle.Id }).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(report => report.Photos, photo =>
        {
            photo.ToTable("symptom_report_photos", "agro");
            photo.WithOwner().HasForeignKey("ReportId");
            photo.Property<Guid>("ReportId");
            photo.HasKey("ReportId", nameof(SymptomReportPhoto.Id));
            photo.Property(item => item.UploadObjectKey).HasMaxLength(1024).IsRequired();
            photo.Property(item => item.ObjectKey).HasMaxLength(1024);
            photo.Property(item => item.ContentType).HasMaxLength(64).IsRequired();
            photo.Property(item => item.ExpectedSizeBytes).IsRequired();
            photo.Property(item => item.ConsentActorUserId).HasMaxLength(450).IsRequired();
            photo.Property(item => item.ConsentNoticeVersion).HasMaxLength(80);
            photo.Property(item => item.DeleteLastError).HasMaxLength(80);
            photo.HasIndex(item => item.UploadObjectKey).IsUnique();
            photo.HasIndex(item => new { item.DeleteAfter, item.DeleteRetryAt, item.DeletedAt });
        });

        builder.OwnsMany(report => report.Decisions, decision =>
        {
            decision.ToTable("symptom_report_decisions", "agro");
            decision.WithOwner().HasForeignKey("ReportId");
            decision.Property<Guid>("ReportId");
            decision.HasKey("ReportId", nameof(SymptomReportDecision.Id));
            decision.Property(item => item.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
            decision.Property(item => item.ReviewerUserId).HasMaxLength(450).IsRequired();
            decision.Property(item => item.RequestedEvidence).HasMaxLength(2000);
            decision.Property(item => item.Diagnosis).HasMaxLength(2000);
            decision.Property(item => item.Treatment).HasMaxLength(4000);
            decision.HasIndex("ReportId", nameof(SymptomReportDecision.DecidedAt));
            decision.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
