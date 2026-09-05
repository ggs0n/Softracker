BEGIN TRANSACTION;
GO

IF COL_LENGTH('WorkTasks', 'Details') IS NULL
    ALTER TABLE [WorkTasks] ADD [Details] nvarchar(4000) NULL;
GO

IF COL_LENGTH('WorkTasks', 'TimelineStart') IS NULL
    ALTER TABLE [WorkTasks] ADD [TimelineStart] datetime2 NULL;
GO

IF COL_LENGTH('WorkTasks', 'TimelineEnd') IS NULL
    ALTER TABLE [WorkTasks] ADD [TimelineEnd] datetime2 NULL;
GO

IF COL_LENGTH('WorkTasks', 'ChangeRequestId') IS NULL
    ALTER TABLE [WorkTasks] ADD [ChangeRequestId] int NULL;
GO

IF COL_LENGTH('WorkTasks', 'BugReportId') IS NULL
    ALTER TABLE [WorkTasks] ADD [BugReportId] int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkTasks_ChangeRequestId' AND object_id = OBJECT_ID('WorkTasks'))
    CREATE INDEX [IX_WorkTasks_ChangeRequestId] ON [WorkTasks] ([ChangeRequestId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkTasks_BugReportId' AND object_id = OBJECT_ID('WorkTasks'))
    CREATE INDEX [IX_WorkTasks_BugReportId] ON [WorkTasks] ([BugReportId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WorkTasks_ChangeRequests_ChangeRequestId')
    ALTER TABLE [WorkTasks] ADD CONSTRAINT [FK_WorkTasks_ChangeRequests_ChangeRequestId]
    FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WorkTasks_BugReports_BugReportId')
    ALTER TABLE [WorkTasks] ADD CONSTRAINT [FK_WorkTasks_BugReports_BugReportId]
    FOREIGN KEY ([BugReportId]) REFERENCES [BugReports] ([Id]) ON DELETE SET NULL;
GO

COMMIT;
GO
