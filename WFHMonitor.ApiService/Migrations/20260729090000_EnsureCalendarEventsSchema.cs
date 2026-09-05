using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WFHMonitor.Data;

#nullable disable

namespace WFHMonitor.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729090000_EnsureCalendarEventsSchema")]
public sealed class EnsureCalendarEventsSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CalendarEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CalendarEvents]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Details] nvarchar(2000) NULL,
        [MeetingLink] nvarchar(1000) NULL,
        [StartAt] datetime2 NOT NULL,
        [EndAt] datetime2 NULL,
        [ExternalSource] nvarchar(50) NULL,
        [ExternalEventId] nvarchar(300) NULL,
        [LastSyncedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_CalendarEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CalendarEvents_AspNetUsers_CreatedById]
            FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[AspNetUsers] ([Id])
            ON DELETE NO ACTION
    );
END;

IF COL_LENGTH('dbo.CalendarEvents', 'ExternalSource') IS NULL
    ALTER TABLE [dbo].[CalendarEvents] ADD [ExternalSource] nvarchar(50) NULL;

IF COL_LENGTH('dbo.CalendarEvents', 'ExternalEventId') IS NULL
    ALTER TABLE [dbo].[CalendarEvents] ADD [ExternalEventId] nvarchar(300) NULL;

IF COL_LENGTH('dbo.CalendarEvents', 'LastSyncedAt') IS NULL
    ALTER TABLE [dbo].[CalendarEvents] ADD [LastSyncedAt] datetime2 NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CalendarEvents_CreatedById'
      AND [object_id] = OBJECT_ID(N'[dbo].[CalendarEvents]')
)
    CREATE INDEX [IX_CalendarEvents_CreatedById]
        ON [dbo].[CalendarEvents] ([CreatedById]);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CalendarEvents_StartAt'
      AND [object_id] = OBJECT_ID(N'[dbo].[CalendarEvents]')
)
    CREATE INDEX [IX_CalendarEvents_StartAt]
        ON [dbo].[CalendarEvents] ([StartAt]);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_CalendarEvents_CreatedById_ExternalSource_ExternalEventId'
      AND [object_id] = OBJECT_ID(N'[dbo].[CalendarEvents]')
)
    CREATE INDEX [IX_CalendarEvents_CreatedById_ExternalSource_ExternalEventId]
        ON [dbo].[CalendarEvents] ([CreatedById], [ExternalSource], [ExternalEventId]);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally preserve calendar data if this repair migration is rolled back.
    }
}
