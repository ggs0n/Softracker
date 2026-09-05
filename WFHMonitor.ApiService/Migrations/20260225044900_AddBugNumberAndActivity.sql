BEGIN TRANSACTION;
GO

ALTER TABLE [BugReports] ADD [BugNumber] nvarchar(20) NOT NULL DEFAULT N'';
GO

CREATE TABLE [BugActivities] (
    [Id] int NOT NULL IDENTITY,
    [BugReportId] int NOT NULL,
    [Action] nvarchar(200) NOT NULL,
    [OldStatus] int NULL,
    [NewStatus] int NULL,
    [OldAssignedDeveloperId] nvarchar(450) NULL,
    [NewAssignedDeveloperId] nvarchar(450) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BugActivities] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BugActivities_AspNetUsers_NewAssignedDeveloperId] FOREIGN KEY ([NewAssignedDeveloperId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BugActivities_AspNetUsers_OldAssignedDeveloperId] FOREIGN KEY ([OldAssignedDeveloperId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BugActivities_BugReports_BugReportId] FOREIGN KEY ([BugReportId]) REFERENCES [BugReports] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_BugReports_BugNumber] ON [BugReports] ([BugNumber]);
GO

CREATE INDEX [IX_BugActivities_BugReportId] ON [BugActivities] ([BugReportId]);
GO

CREATE INDEX [IX_BugActivities_CreatedAt] ON [BugActivities] ([CreatedAt]);
GO

CREATE INDEX [IX_BugActivities_NewAssignedDeveloperId] ON [BugActivities] ([NewAssignedDeveloperId]);
GO

CREATE INDEX [IX_BugActivities_OldAssignedDeveloperId] ON [BugActivities] ([OldAssignedDeveloperId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225044900_AddBugNumberAndActivity', N'8.0.0');
GO

COMMIT;
GO

