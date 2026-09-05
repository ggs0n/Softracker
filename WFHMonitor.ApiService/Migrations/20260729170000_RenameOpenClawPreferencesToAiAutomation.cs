using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WFHMonitor.Data;

#nullable disable

namespace WFHMonitor.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729170000_RenameOpenClawPreferencesToAiAutomation")]
public sealed class RenameOpenClawPreferencesToAiAutomation
    : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'EnableOpenClawAgents') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'EnableAiAutomation') IS NULL
                EXEC sp_rename
                    N'dbo.SystemPreferences.EnableOpenClawAgents',
                    N'EnableAiAutomation',
                    N'COLUMN';

            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'AllowOpenClawForFreePlan') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'AllowAiAutomationForFreePlan') IS NULL
                EXEC sp_rename
                    N'dbo.SystemPreferences.AllowOpenClawForFreePlan',
                    N'AllowAiAutomationForFreePlan',
                    N'COLUMN';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'EnableAiAutomation') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'EnableOpenClawAgents') IS NULL
                EXEC sp_rename
                    N'dbo.SystemPreferences.EnableAiAutomation',
                    N'EnableOpenClawAgents',
                    N'COLUMN';

            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'AllowAiAutomationForFreePlan') IS NOT NULL
               AND COL_LENGTH('dbo.SystemPreferences', 'AllowOpenClawForFreePlan') IS NULL
                EXEC sp_rename
                    N'dbo.SystemPreferences.AllowAiAutomationForFreePlan',
                    N'AllowOpenClawForFreePlan',
                    N'COLUMN';
            """);
    }
}
