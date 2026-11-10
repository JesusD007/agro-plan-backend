using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class TaskReviewDecisionConfiguration : IEntityTypeConfiguration<TaskReviewDecision>
{
    public void Configure(EntityTypeBuilder<TaskReviewDecision> builder)
    {
        builder.ToTable("task_review_decisions", "agro");
        builder.HasKey(decision => decision.Id);
        builder.Property(decision => decision.ReviewerUserId).HasMaxLength(450).IsRequired();
        builder.Property(decision => decision.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(decision => decision.Reason).HasMaxLength(1000);
        builder.HasIndex(decision => new { decision.FarmId, decision.WorkTaskId, decision.DecidedAt });
        builder.HasOne<WorkTask>().WithMany()
            .HasForeignKey(decision => new { decision.FarmId, decision.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<TaskEvidenceSubmission>().WithMany()
            .HasForeignKey(decision => new { decision.FarmId, decision.WorkTaskId, decision.SubmissionId })
            .HasPrincipalKey(submission => new { submission.FarmId, submission.WorkTaskId, submission.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AgroPlan.Infrastructure.Identity.ApplicationUser>().WithMany()
            .HasForeignKey(decision => decision.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
