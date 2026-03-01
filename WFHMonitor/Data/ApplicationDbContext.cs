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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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

            e.HasOne(c => c.CreatedBy)
             .WithMany()
             .HasForeignKey(c => c.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
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
            e.HasIndex(b => b.AssignedDeveloperId);
            e.HasIndex(b => b.ChangeRequestId);
            e.HasIndex(b => b.AssigneeType);
            e.HasIndex(b => b.AgentStatus);
            e.HasIndex(b => b.BugNumber).IsUnique();

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

            e.HasOne(c => c.CreatedBy)
             .WithMany()
             .HasForeignKey(c => c.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
