using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL with IF NOT EXISTS to safely create indexes that may already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkTasks_CreatedById' AND object_id = OBJECT_ID('WorkTasks'))
                    CREATE INDEX IX_WorkTasks_CreatedById ON WorkTasks (CreatedById);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChangeRequests_CreatedById' AND object_id = OBJECT_ID('ChangeRequests'))
                    CREATE INDEX IX_ChangeRequests_CreatedById ON ChangeRequests (CreatedById);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChangeRequests_OrgTeamId' AND object_id = OBJECT_ID('ChangeRequests'))
                    CREATE INDEX IX_ChangeRequests_OrgTeamId ON ChangeRequests (OrgTeamId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BugReports_CreatedById' AND object_id = OBJECT_ID('BugReports'))
                    CREATE INDEX IX_BugReports_CreatedById ON BugReports (CreatedById);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserNotifications_RecipientId_CreatedAt' AND object_id = OBJECT_ID('UserNotifications'))
                    CREATE INDEX IX_UserNotifications_RecipientId_CreatedAt ON UserNotifications (RecipientId, CreatedAt);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_CompanyName' AND object_id = OBJECT_ID('AspNetUsers'))
                    CREATE INDEX IX_AspNetUsers_CompanyName ON AspNetUsers (CompanyName);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS IX_WorkTasks_CreatedById ON WorkTasks;
                DROP INDEX IF EXISTS IX_ChangeRequests_CreatedById ON ChangeRequests;
                DROP INDEX IF EXISTS IX_ChangeRequests_OrgTeamId ON ChangeRequests;
                DROP INDEX IF EXISTS IX_BugReports_CreatedById ON BugReports;
                DROP INDEX IF EXISTS IX_UserNotifications_RecipientId_CreatedAt ON UserNotifications;
                DROP INDEX IF EXISTS IX_AspNetUsers_CompanyName ON AspNetUsers;
            ");
        }
    }
}
