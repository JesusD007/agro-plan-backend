using AgroPlan.Domain.Workforce;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class WorkerInvitationConfiguration : IEntityTypeConfiguration<WorkerInvitation>
{
    public void Configure(EntityTypeBuilder<WorkerInvitation> builder)
    {
        builder.ToTable("worker_invitations", "agro");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Email).HasMaxLength(256).IsRequired();
        builder.Property(invitation => invitation.TokenDigest).HasMaxLength(64).IsRequired();
        builder.Property(invitation => invitation.IssuedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(invitation => invitation.AcceptedByUserId).HasMaxLength(450);
        builder.HasIndex(invitation => invitation.TokenDigest).IsUnique();
        builder.HasIndex(invitation => new { invitation.FarmId, invitation.Email, invitation.ExpiresAt });
        builder.HasOne<AgroPlan.Domain.Farms.Farm>().WithMany().HasForeignKey(invitation => invitation.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AgroPlan.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AgroPlan.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
