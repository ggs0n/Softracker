using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WFHMonitor.Data;

#nullable disable

namespace WFHMonitor.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260309010000_AddOutlookCalendarSync")]
    public partial class AddOutlookCalendarSync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF COL_LENGTH('AspNetUsers', 'OutlookCalendarLastSyncAt') IS NULL
BEGIN
    ALTER TABLE [AspNetUsers] ADD [OutlookCalendarLastSyncAt] datetime2 NULL;
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('AspNetUsers', 'OutlookCalendarIcsUrl') IS NULL
BEGIN
    ALTER TABLE [AspNetUsers] ADD [OutlookCalendarIcsUrl] nvarchar(max) NULL;
END
""");

            migrationBuilder.Sql("""
IF OBJECT_ID('CalendarEvents', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('CalendarEvents', 'ExternalEventId') IS NULL
        ALTER TABLE [CalendarEvents] ADD [ExternalEventId] nvarchar(300) NULL;

    IF COL_LENGTH('CalendarEvents', 'ExternalSource') IS NULL
        ALTER TABLE [CalendarEvents] ADD [ExternalSource] nvarchar(50) NULL;

    IF COL_LENGTH('CalendarEvents', 'LastSyncedAt') IS NULL
        ALTER TABLE [CalendarEvents] ADD [LastSyncedAt] datetime2 NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_CalendarEvents_CreatedById_ExternalSource_ExternalEventId'
          AND object_id = OBJECT_ID('CalendarEvents')
    )
    BEGIN
        CREATE INDEX [IX_CalendarEvents_CreatedById_ExternalSource_ExternalEventId]
            ON [CalendarEvents] ([CreatedById], [ExternalSource], [ExternalEventId]);
    END
END
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF OBJECT_ID('CalendarEvents', 'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_CalendarEvents_CreatedById_ExternalSource_ExternalEventId'
          AND object_id = OBJECT_ID('CalendarEvents')
    )
        DROP INDEX [IX_CalendarEvents_CreatedById_ExternalSource_ExternalEventId] ON [CalendarEvents];

    IF COL_LENGTH('CalendarEvents', 'LastSyncedAt') IS NOT NULL
        ALTER TABLE [CalendarEvents] DROP COLUMN [LastSyncedAt];

    IF COL_LENGTH('CalendarEvents', 'ExternalSource') IS NOT NULL
        ALTER TABLE [CalendarEvents] DROP COLUMN [ExternalSource];

    IF COL_LENGTH('CalendarEvents', 'ExternalEventId') IS NOT NULL
        ALTER TABLE [CalendarEvents] DROP COLUMN [ExternalEventId];
END
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('AspNetUsers', 'OutlookCalendarIcsUrl') IS NOT NULL
    ALTER TABLE [AspNetUsers] DROP COLUMN [OutlookCalendarIcsUrl];
""");

            migrationBuilder.Sql("""
IF COL_LENGTH('AspNetUsers', 'OutlookCalendarLastSyncAt') IS NOT NULL
    ALTER TABLE [AspNetUsers] DROP COLUMN [OutlookCalendarLastSyncAt];
""");
        }
    }
}
