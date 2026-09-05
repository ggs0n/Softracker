BEGIN TRANSACTION;
GO

IF OBJECT_ID(N'[CalendarEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [CalendarEvents] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Details] nvarchar(2000) NULL,
        [MeetingLink] nvarchar(1000) NULL,
        [StartAt] datetime2 NOT NULL,
        [EndAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_CalendarEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CalendarEvents_AspNetUsers_CreatedById]
            FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CalendarEvents_CreatedById' AND object_id = OBJECT_ID('CalendarEvents'))
    CREATE INDEX [IX_CalendarEvents_CreatedById] ON [CalendarEvents] ([CreatedById]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CalendarEvents_StartAt' AND object_id = OBJECT_ID('CalendarEvents'))
    CREATE INDEX [IX_CalendarEvents_StartAt] ON [CalendarEvents] ([StartAt]);
GO

COMMIT;
GO
