using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WFHMonitor.Migrations
{
    /// <inheritdoc />
    public partial class AddBugSeverityLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('BugReports', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('BugReports', 'Severity') IS NULL
        ALTER TABLE [BugReports] ADD [Severity] nvarchar(20) NOT NULL CONSTRAINT [DF_BugReports_Severity] DEFAULT N'Medium';

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_BugReports_Severity'
          AND object_id = OBJECT_ID('BugReports')
    )
        CREATE INDEX [IX_BugReports_Severity] ON [BugReports] ([Severity]);
END
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('BugReports', 'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_BugReports_Severity'
          AND object_id = OBJECT_ID('BugReports')
    )
        DROP INDEX [IX_BugReports_Severity] ON [BugReports];

    IF COL_LENGTH('BugReports', 'Severity') IS NOT NULL
    BEGIN
        DECLARE @dfName nvarchar(256);
        SELECT @dfName = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('BugReports')
          AND c.name = 'Severity';

        IF @dfName IS NOT NULL
            EXEC('ALTER TABLE [BugReports] DROP CONSTRAINT [' + @dfName + ']');

        ALTER TABLE [BugReports] DROP COLUMN [Severity];
    END
END
""");
        }
    }
}
