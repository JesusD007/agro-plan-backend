using AgroPlan.Domain.Farms;
using AgroPlan.Domain.Crops;
using AgroPlan.Domain.Planning;
using AgroPlan.Domain.Logbook;
using AgroPlan.Domain.Calendars;
using AgroPlan.Domain.Operations;
using AgroPlan.Domain.Safety;
using AgroPlan.Domain.AdvisoryAccess;
using AgroPlan.Domain.Workforce;
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
    public DbSet<Parcel> Parcels => Set<Parcel>();
    public DbSet<FarmMembershipAuditEntry> FarmMembershipAuditEntries => Set<FarmMembershipAuditEntry>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<RecoveryRequest> RecoveryRequests => Set<RecoveryRequest>();
    public DbSet<RecoveryAuditEntry> RecoveryAuditEntries => Set<RecoveryAuditEntry>();
    public DbSet<CropCatalogEntry> CropCatalogEntries => Set<CropCatalogEntry>();
    public DbSet<AgronomicParameter> AgronomicParameters => Set<AgronomicParameter>();
    public DbSet<CropFramework> CropFrameworks => Set<CropFramework>();
    public DbSet<CropCycle> CropCycles => Set<CropCycle>();
    public DbSet<SowingProposal> SowingProposals => Set<SowingProposal>();
    public DbSet<PlantingFrameProposal> PlantingFrameProposals => Set<PlantingFrameProposal>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LogbookEntry> LogbookEntries => Set<LogbookEntry>();
    public DbSet<LogbookAttachment> LogbookAttachments => Set<LogbookAttachment>();
    public DbSet<ScheduleChangeProposal> ScheduleChangeProposals => Set<ScheduleChangeProposal>();
    public DbSet<CalendarScheduleItem> CalendarScheduleItems => Set<CalendarScheduleItem>();
    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<IrrigationEntry> IrrigationEntries => Set<IrrigationEntry>();
    public DbSet<InputApplicationEntry> InputApplicationEntries => Set<InputApplicationEntry>();
    public DbSet<ValidatedApplicationFrequencyRule> ValidatedApplicationFrequencyRules => Set<ValidatedApplicationFrequencyRule>();
    public DbSet<ValidatedProductLabel> ValidatedProductLabels => Set<ValidatedProductLabel>();
    public DbSet<HarvestEntry> HarvestEntries => Set<HarvestEntry>();
    public DbSet<CropCalendarTemplate> CropCalendarTemplates => Set<CropCalendarTemplate>();
    public DbSet<AdvisorAccessGrant> AdvisorAccessGrants => Set<AdvisorAccessGrant>();
    public DbSet<WorkerInvitation> WorkerInvitations => Set<WorkerInvitation>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();

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
