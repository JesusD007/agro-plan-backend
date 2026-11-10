using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class VerifiedWorkRewardConfiguration : IEntityTypeConfiguration<VerifiedWorkReward>
{
    public void Configure(EntityTypeBuilder<VerifiedWorkReward> builder)
    {
        builder.ToTable("verified_work_rewards", "agro", table =>
        {
            table.HasCheckConstraint("CK_verified_work_rewards_Points", "\"Points\" >= 0");
            table.HasCheckConstraint("CK_verified_work_rewards_RuleVersion", "\"RuleVersion\" > 0");
        });
        builder.HasKey(reward => reward.Id);
        builder.HasAlternateKey(reward => new { reward.FarmId, reward.Id });
        builder.Property(reward => reward.WorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(reward => reward.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(reward => reward.RuleKey).HasMaxLength(100).IsRequired();
        builder.Property(reward => reward.Description).HasMaxLength(1000);
        builder.Property(reward => reward.GrantedByUserId).HasMaxLength(450);
        builder.HasIndex(reward => new { reward.FarmId, reward.WorkTaskId, reward.Kind, reward.RuleKey }).IsUnique()
            .HasFilter("\"WorkTaskId\" IS NOT NULL");
        builder.HasIndex(reward => new { reward.FarmId, reward.WorkerUserId, reward.AwardedAt });
        builder.HasIndex(reward => new { reward.FarmId, reward.WorkerUserId, reward.Kind, reward.RuleKey }).IsUnique()
            .HasFilter("\"Kind\" = 'Badge'");
        builder.HasOne<WorkTask>().WithMany().HasForeignKey(reward => new { reward.FarmId, reward.WorkTaskId })
            .HasPrincipalKey(task => new { task.FarmId, task.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(reward => new { reward.FarmId, UserId = reward.WorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(reward => new { reward.FarmId, UserId = reward.GrantedByUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
