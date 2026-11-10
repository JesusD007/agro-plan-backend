using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class TaskEvidenceSubmissionConfiguration : IEntityTypeConfiguration<TaskEvidenceSubmission>
{
    public void Configure(EntityTypeBuilder<TaskEvidenceSubmission> builder)
    {
        builder.ToTable("task_evidence_submissions", "agro");
        builder.HasKey(submission => submission.Id);
        builder.HasAlternateKey(submission => new { submission.FarmId, submission.WorkTaskId, submission.Id });
        builder.Property(submission => submission.WorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(submission => submission.UploadObjectKey).HasMaxLength(1024).IsRequired();
        builder.Property(submission => submission.ObjectKey).HasMaxLength(1024);
        builder.Property(submission => submission.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(submission => submission.Location).HasMaxLength(256);
        builder.HasIndex(submission => new { submission.FarmId, submission.WorkTaskId, submission.Revision }).IsUnique();
        builder.HasIndex(submission => new { submission.WorkTaskId, submission.ClientOperationId }).IsUnique();
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(submission => new { submission.FarmId, submission.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(submission => new { submission.FarmId, UserId = submission.WorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskEvidenceSubmission>().WithMany()
            .HasForeignKey(submission => new { submission.FarmId, submission.WorkTaskId, submission.PreviousSubmissionId })
            .HasPrincipalKey(previous => new { previous.FarmId, previous.WorkTaskId, previous.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
