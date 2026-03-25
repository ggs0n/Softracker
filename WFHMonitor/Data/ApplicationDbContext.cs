using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Models;

namespace WFHMonitor.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<EodReport> EodReports => Set<EodReport>();
    public DbSet<EodReportTask> EodReportTasks => Set<EodReportTask>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeRequestPic> ChangeRequestPics => Set<ChangeRequestPic>();
    public DbSet<ArchSpecImage> ArchSpecImages => Set<ArchSpecImage>();
    public DbSet<ChangeRequestDocument> ChangeRequestDocuments => Set<ChangeRequestDocument>();
    public DbSet<BugReport> BugReports => Set<BugReport>();
    public DbSet<BugScreenshot> BugScreenshots => Set<BugScreenshot>();
    public DbSet<BugDocument> BugDocuments => Set<BugDocument>();
    public DbSet<BugActivity> BugActivities => Set<BugActivity>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<ProjectFeature> ProjectFeatures => Set<ProjectFeature>();
    public DbSet<FeatureScreenshot> FeatureScreenshots => Set<FeatureScreenshot>();
    public DbSet<RepositoryFeature> RepositoryFeatures => Set<RepositoryFeature>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<ModulePermissionSetting> ModulePermissionSettings => Set<ModulePermissionSetting>();
    public DbSet<SystemPreference> SystemPreferences => Set<SystemPreference>();
    public DbSet<UserOnboardingState> UserOnboardingStates => Set<UserOnboardingState>();
    public DbSet<OrgTeam> OrgTeams => Set<OrgTeam>();
    public DbSet<OrganizationProfile> OrganizationProfiles => Set<OrganizationProfile>();
    public DbSet<TestCase> TestCases => Set<TestCase>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.SubscriptionPlan)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(SubscriptionPlan.Free);

            e.Property(u => u.OrganizationTeam)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(OrganizationTeam.Unassigned);

            e.Property(u => u.CompanyName)
                .HasMaxLength(200);

            e.Property(u => u.ProfilePhotoPath)
                .HasMaxLength(300);

            e.Property(u => u.IsProSubscriptionActive)
                .HasDefaultValue(false);

            e.Property(u => u.ProSubscriptionEndsAt);

            e.Property(u => u.IsProCancelAtPeriodEnd)
                .HasDefaultValue(false);

            e.Property(u => u.StripeCustomerId)
                .HasMaxLength(100);

            e.Property(u => u.StripeSubscriptionId)
                .HasMaxLength(100);

            e.Property(u => u.LastProcessedStripeCheckoutSessionId)
                .HasMaxLength(200);

            e.Property(u => u.PendingStripeCheckoutSessionId)
                .HasMaxLength(200);

            e.Property(u => u.PendingStripeCheckoutUrl)
                .HasMaxLength(1000);

            e.Property(u => u.PendingStripeCheckoutCreatedAt);

            e.HasOne(u => u.OrgTeam)
                .WithMany()
                .HasForeignKey(u => u.OrgTeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<WorkTask>(e =>
        {
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.AssigneeId);
            e.HasIndex(t => t.DueDate);
            e.HasIndex(t => t.ChangeRequestId);
            e.HasIndex(t => t.BugReportId);

            e.HasOne(t => t.Assignee)
             .WithMany(u => u.AssignedTasks)
             .HasForeignKey(t => t.AssigneeId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.CreatedBy)
             .WithMany()
             .HasForeignKey(t => t.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(t => t.ChangeRequest)
             .WithMany()
             .HasForeignKey(t => t.ChangeRequestId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.BugReport)
             .WithMany()
             .HasForeignKey(t => t.BugReportId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EodReport>(e =>
        {
            e.HasIndex(r => r.EmployeeId);
            e.HasIndex(r => r.ReportDate);
            e.HasIndex(r => new { r.EmployeeId, r.ReportDate }).IsUnique();

            e.HasOne(r => r.Employee)
             .WithMany(u => u.EodReports)
             .HasForeignKey(r => r.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EodReportTask>(e =>
        {
            e.HasIndex(t => t.EodReportId);
            e.HasIndex(t => t.TaskId);
        });

        builder.Entity<ChangeRequest>(e =>
        {
            e.HasIndex(c => c.CrNumber).IsUnique();
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.BugScanStatus);

            e.HasOne(c => c.CreatedBy)
             .WithMany()
             .HasForeignKey(c => c.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.OrgTeam)
             .WithMany()
             .HasForeignKey(c => c.OrgTeamId)
             .OnDelete(DeleteBehavior.SetNull);

            e.Property(c => c.BugScanStatus)
             .HasConversion<string>()
             .HasMaxLength(20)
             .HasDefaultValue(ProjectBugScanStatus.None);

            e.Property(c => c.BugScanAgentId)
             .HasMaxLength(100);

            e.Property(c => c.BugScanLastMessage)
             .HasMaxLength(500);
        });

        builder.Entity<ChangeRequestPic>(e =>
        {
            e.HasIndex(p => p.ChangeRequestId);

            e.HasOne(p => p.ChangeRequest)
             .WithMany(c => c.Pics)
             .HasForeignKey(p => p.ChangeRequestId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Employee)
             .WithMany()
             .HasForeignKey(p => p.EmployeeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ArchSpecImage>(e =>
        {
            e.HasIndex(i => i.ChangeRequestId);

            e.HasOne(i => i.ChangeRequest)
             .WithMany(c => c.ArchSpecImages)
             .HasForeignKey(i => i.ChangeRequestId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChangeRequestDocument>(e =>
        {
            e.HasIndex(d => d.ChangeRequestId);

            e.HasOne(d => d.ChangeRequest)
             .WithMany(c => c.Documents)
             .HasForeignKey(d => d.ChangeRequestId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BugReport>(e =>
        {
            e.HasIndex(b => b.Status);
            e.HasIndex(b => b.Severity);
            e.HasIndex(b => b.AssignedDeveloperId);
            e.HasIndex(b => b.ChangeRequestId);
            e.HasIndex(b => b.AssigneeType);
            e.HasIndex(b => b.AgentStatus);
            e.HasIndex(b => b.BugNumber).IsUnique();

            e.Property(b => b.Severity)
             .HasConversion<string>()
             .HasMaxLength(20)
             .HasDefaultValue(BugSeverity.Medium);

            e.HasOne(b => b.CreatedBy)
             .WithMany()
             .HasForeignKey(b => b.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.AssignedDeveloper)
             .WithMany()
             .HasForeignKey(b => b.AssignedDeveloperId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(b => b.ChangeRequest)
             .WithMany()
             .HasForeignKey(b => b.ChangeRequestId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<BugScreenshot>(e =>
        {
            e.HasIndex(s => s.BugReportId);

            e.HasOne(s => s.BugReport)
             .WithMany(b => b.Screenshots)
             .HasForeignKey(s => s.BugReportId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BugDocument>(e =>
        {
            e.HasIndex(d => d.BugReportId);

            e.HasOne(d => d.BugReport)
             .WithMany(b => b.Documents)
             .HasForeignKey(d => d.BugReportId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BugActivity>(e =>
        {
            e.HasIndex(a => a.BugReportId);
            e.HasIndex(a => a.CreatedAt);

            e.HasOne(a => a.BugReport)
             .WithMany(b => b.Activities)
             .HasForeignKey(a => a.BugReportId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.OldAssignedDeveloper)
             .WithMany()
             .HasForeignKey(a => a.OldAssignedDeveloperId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.NewAssignedDeveloper)
             .WithMany()
             .HasForeignKey(a => a.NewAssignedDeveloperId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(c => c.StartAt);
            e.HasIndex(c => c.CreatedById);
            e.HasIndex(c => new { c.CreatedById, c.ExternalSource, c.ExternalEventId });

            e.HasOne(c => c.CreatedBy)
             .WithMany()
             .HasForeignKey(c => c.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProjectFeature>(e =>
        {
            e.HasIndex(f => f.ChangeRequestId);
            e.HasIndex(f => f.AssignedDeveloperId);
            e.HasIndex(f => f.AgentStatus);
            e.HasIndex(f => f.FeatureNumber).IsUnique();

            e.Property(f => f.FeatureNumber)
             .HasMaxLength(20);

            e.Property(f => f.ModuleImpacted)
             .HasMaxLength(200);

            e.Property(f => f.LinkedBugs)
             .HasMaxLength(1000);

            e.Property(f => f.Status)
             .HasConversion<string>()
             .HasMaxLength(20)
             .HasDefaultValue(CrStatus.Draft);

            e.Property(f => f.Priority)
             .HasConversion<string>()
             .HasMaxLength(20)
             .HasDefaultValue(CrPriority.Medium);

            e.Property(f => f.Stage)
             .HasConversion<string>()
             .HasMaxLength(30)
             .HasDefaultValue(CrStage.Development);

            e.Property(f => f.AgentStatus)
             .HasConversion<string>()
             .HasMaxLength(20)
             .HasDefaultValue(FeatureAgentStatus.None);

            e.Property(f => f.AgentImplementationPlan)
             .HasMaxLength(4000);

            e.HasOne(f => f.ChangeRequest)
             .WithMany(c => c.Features)
             .HasForeignKey(f => f.ChangeRequestId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(f => f.AssignedDeveloper)
             .WithMany()
             .HasForeignKey(f => f.AssignedDeveloperId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<FeatureScreenshot>(e =>
        {
            e.HasIndex(s => s.ProjectFeatureId);

            e.HasOne(s => s.ProjectFeature)
             .WithMany(f => f.Screenshots)
             .HasForeignKey(s => s.ProjectFeatureId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RepositoryFeature>(e =>
        {
            e.HasIndex(f => f.ChangeRequestId);
            e.HasIndex(f => new { f.ChangeRequestId, f.Name }).IsUnique();

            e.HasOne(f => f.ChangeRequest)
             .WithMany(c => c.RepositoryFeatures)
             .HasForeignKey(f => f.ChangeRequestId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserNotification>(e =>
        {
            e.HasIndex(n => n.RecipientId);
            e.HasIndex(n => new { n.RecipientId, n.IsRead });
            e.HasIndex(n => n.CreatedAt);

            e.HasOne(n => n.Recipient)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.RecipientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModulePermissionSetting>(e =>
        {
            e.HasIndex(m => m.ModuleKey).IsUnique();

            e.Property(m => m.ModuleKey)
                .HasMaxLength(50);

            e.Property(m => m.ViewRolesCsv)
                .HasMaxLength(400);

            e.Property(m => m.ModifyRolesCsv)
                .HasMaxLength(400);
        });

        builder.Entity<SystemPreference>(e =>
        {
            e.HasKey(s => s.Id);

            e.Property(s => s.BellNotificationSoundEnabled)
                .HasDefaultValue(true);

            e.Property(s => s.BellNotificationSoundOption)
                .HasMaxLength(30)
                .HasDefaultValue(BellSoundOptions.Classic);

            e.Property(s => s.FreeProjectLimit)
                .HasDefaultValue(ProVersionDefaults.FreeProjectLimit);

            e.Property(s => s.FreeBugLimit)
                .HasDefaultValue(ProVersionDefaults.FreeBugLimit);

            e.Property(s => s.FreeFeatureLimit)
                .HasDefaultValue(ProVersionDefaults.FreeFeatureLimit);

            e.Property(s => s.EnableOpenClawAgents)
                .HasDefaultValue(true);

            e.Property(s => s.AllowOpenClawForFreePlan)
                .HasDefaultValue(false);
        });

        builder.Entity<UserOnboardingState>(e =>
        {
            e.HasKey(s => s.UserId);

            e.Property(s => s.UserId)
                .HasMaxLength(450);

            e.Property(s => s.LastSeenStepKey)
                .HasMaxLength(50)
                .HasDefaultValue(OnboardingStepKeys.AddProject);

            e.Property(s => s.IsDismissed)
                .HasDefaultValue(false);

            e.Property(s => s.IsCompleted)
                .HasDefaultValue(false);

            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrgTeam>(e =>
        {
            e.HasIndex(t => new { t.CompanyName, t.Name }).IsUnique();

            e.Property(t => t.Name)
                .HasMaxLength(100);

            e.Property(t => t.CompanyName)
                .HasMaxLength(200);
        });

        builder.Entity<TestCase>(e =>
        {
            e.HasIndex(t => t.TestNumber).IsUnique();
            e.HasIndex(t => t.ChangeRequestId);
            e.HasIndex(t => t.LinkedBugId);
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.Category);
            e.HasIndex(t => t.Module);

            e.Property(t => t.TestNumber).HasMaxLength(20);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TestCaseStatus.Pending);
            e.Property(t => t.Category).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TestCaseCategory.All);
            e.Property(t => t.Environment).HasConversion<string>().HasMaxLength(20).HasDefaultValue(TestCaseEnvironment.Dev);

            e.HasOne(t => t.ChangeRequest)
             .WithMany()
             .HasForeignKey(t => t.ChangeRequestId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.LinkedBug)
             .WithMany()
             .HasForeignKey(t => t.LinkedBugId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.CreatedBy)
             .WithMany()
             .HasForeignKey(t => t.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrganizationProfile>(e =>
        {
            e.HasKey(p => p.Id);

            e.HasOne(p => p.CeoUser)
                .WithMany()
                .HasForeignKey(p => p.CeoUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
