using AgroPlan.Domain.Farms;
using AgroPlan.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgroPlan.Infrastructure.Persistence;

public sealed class AgroPlanDbContext(DbContextOptions<AgroPlanDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<FarmMembership> FarmMemberships => Set<FarmMembership>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<RecoveryRequest> RecoveryRequests => Set<RecoveryRequest>();
    public DbSet<RecoveryAuditEntry> RecoveryAuditEntries => Set<RecoveryAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().ToTable("AspNetUsers", "identity");
        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex");
        builder.Entity<IdentityRole>().ToTable("AspNetRoles", "identity");
        builder.Entity<IdentityUserClaim<string>>().ToTable("AspNetUserClaims", "identity");
        builder.Entity<IdentityUserLogin<string>>().ToTable("AspNetUserLogins", "identity");
        builder.Entity<IdentityUserToken<string>>().ToTable("AspNetUserTokens", "identity");
        builder.Entity<IdentityUserRole<string>>().ToTable("AspNetUserRoles", "identity");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims", "identity");
        builder.ApplyConfigurationsFromAssembly(typeof(AgroPlanDbContext).Assembly);
    }
}
