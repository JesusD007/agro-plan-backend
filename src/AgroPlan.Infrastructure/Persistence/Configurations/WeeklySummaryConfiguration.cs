using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Reports;
using AgroPlan.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class WeeklySummaryConfiguration : IEntityTypeConfiguration<WeeklySummary>
{
    public void Configure(EntityTypeBuilder<WeeklySummary> builder)
    {
        builder.ToTable("weekly_summaries", "agro");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.FarmId, x.Id });
        builder.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RequestedWeekAnchorDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.WeekStartDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.WeekEndDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProjectionJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.NarrativeStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.NarrativeText).HasMaxLength(16_384);
        builder.Property(x => x.NarrativeSourceIds).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SharedAdvisorUserId).HasMaxLength(450);
        builder.Property(x => x.SharedByUserId).HasMaxLength(450);
        builder.Property(x => x.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(x => new { x.FarmId, x.WeekStartDate });
        builder.HasIndex(x => new { x.SharedAdvisorUserId, x.FarmId, x.SharedAt });
        builder.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropCycle>().WithMany().HasForeignKey(x => new { x.FarmId, x.CropCycleId })
            .HasPrincipalKey(x => new { x.FarmId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SharedAdvisorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SharedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
