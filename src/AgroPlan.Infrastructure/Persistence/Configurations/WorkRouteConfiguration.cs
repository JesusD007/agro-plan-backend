using AgroPlan.Domain.Workforce;
using AgroPlan.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgroPlan.Infrastructure.Persistence.Configurations;

public sealed class WorkRouteConfiguration : IEntityTypeConfiguration<WorkRoute>
{
    public void Configure(EntityTypeBuilder<WorkRoute> builder)
    {
        builder.ToTable("work_routes", "agro");
        builder.HasKey(route => route.Id);
        builder.HasAlternateKey(route => new { route.FarmId, route.RouteId, route.Version });
        builder.Property(route => route.WorkerUserId).HasMaxLength(450).IsRequired();
        builder.Property(route => route.Version).IsRequired();
        builder.Property(route => route.OrderedTaskIdsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(route => route.TaskSourcesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(route => route.GeometryGeoJson).HasColumnType("jsonb");
        builder.Property(route => route.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(route => route.CreatedAt).IsRequired();
        builder.Property(route => route.ConfirmedByUserId).HasMaxLength(450);
        builder.HasIndex(route => new { route.FarmId, route.WorkerUserId, route.RouteId, route.Version });
        builder.HasOne<Farm>().WithMany().HasForeignKey(route => route.FarmId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(route => new { route.FarmId, UserId = route.WorkerUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FarmMembership>().WithMany()
            .HasForeignKey(route => new { route.FarmId, UserId = route.ConfirmedByUserId })
            .HasPrincipalKey(member => new { member.FarmId, member.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
