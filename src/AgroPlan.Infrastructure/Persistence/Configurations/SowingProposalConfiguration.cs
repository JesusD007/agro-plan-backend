using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class SowingProposalConfiguration : IEntityTypeConfiguration<SowingProposal>
{
    public void Configure(EntityTypeBuilder<SowingProposal> builder)
    {
        builder.ToTable("sowing_proposals", "agro");
        builder.HasKey(proposal => proposal.Id);
        builder.HasAlternateKey(proposal => new { proposal.FarmId, proposal.Id });
        builder.Property(proposal => proposal.SubmittedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(proposal => proposal.InputSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(proposal => proposal.ResultJson).HasColumnType("jsonb");
        builder.Property(proposal => proposal.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(proposal => proposal.FailureCode).HasMaxLength(80);
        builder.Property(proposal => proposal.FailureSummary).HasMaxLength(400);
        builder.Property(proposal => proposal.ConfirmedByUserId).HasMaxLength(450);
        builder.Property(proposal => proposal.StatusVersion).IsConcurrencyToken();
        builder.Property(proposal => proposal.CreatedAt).IsRequired();
        builder.Property(proposal => proposal.UpdatedAt).IsRequired();
        builder.Property(proposal => proposal.StatusChangedAt).IsRequired();
        builder.HasIndex(proposal => new { proposal.FarmId, proposal.Status, proposal.CreatedAt });
        builder.HasIndex(proposal => new { proposal.NotifiedStatusVersion, proposal.StatusChangedAt });
        builder.HasOne<CropCycle>()
            .WithMany()
            .HasForeignKey(proposal => new { proposal.FarmId, proposal.CropCycleId })
            .HasPrincipalKey(cycle => new { cycle.FarmId, cycle.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Farm>()
            .WithMany()
            .HasForeignKey(proposal => proposal.FarmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
