using AgroPlan.Domain.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class PlantingFrameProposalConfiguration : IEntityTypeConfiguration<PlantingFrameProposal>
{
    public void Configure(EntityTypeBuilder<PlantingFrameProposal> builder)
    {
        builder.ToTable("planting_frame_proposals", "agro");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.SubmittedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.ConfirmedByUserId).HasMaxLength(450);
        builder.Property(item => item.InputSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.ResultJson).HasColumnType("jsonb");
        builder.HasIndex(item => new { item.FarmId, item.CreatedAt });
        builder.HasIndex(item => new { item.Status, item.UpdatedAt });
    }
}
