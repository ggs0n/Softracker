BEGIN TRANSACTION;
GO

CREATE TABLE [BugReports] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(300) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [Workflow] nvarchar(4000) NULL,
    [StepsToReproduce] nvarchar(4000) NULL,
    [ModuleImpacted] nvarchar(200) NULL,
    [Status] int NOT NULL,
    [AssignedDeveloperId] nvarchar(450) NULL,
    [CreatedById] nvarchar(450) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BugReports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BugReports_AspNetUsers_AssignedDeveloperId] FOREIGN KEY ([AssignedDeveloperId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_BugReports_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BugDocuments] (
    [Id] int NOT NULL IDENTITY,
    [BugReportId] int NOT NULL,
    [FileName] nvarchar(300) NOT NULL,
    [OriginalFileName] nvarchar(300) NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BugDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BugDocuments_BugReports_BugReportId] FOREIGN KEY ([BugReportId]) REFERENCES [BugReports] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [BugScreenshots] (
    [Id] int NOT NULL IDENTITY,
    [BugReportId] int NOT NULL,
    [FileName] nvarchar(300) NOT NULL,
    [OriginalFileName] nvarchar(300) NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_BugScreenshots] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BugScreenshots_BugReports_BugReportId] FOREIGN KEY ([BugReportId]) REFERENCES [BugReports] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_BugDocuments_BugReportId] ON [BugDocuments] ([BugReportId]);
GO

CREATE INDEX [IX_BugReports_AssignedDeveloperId] ON [BugReports] ([AssignedDeveloperId]);
GO

CREATE INDEX [IX_BugReports_CreatedById] ON [BugReports] ([CreatedById]);
GO

CREATE INDEX [IX_BugReports_Status] ON [BugReports] ([Status]);
GO

CREATE INDEX [IX_BugScreenshots_BugReportId] ON [BugScreenshots] ([BugReportId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225042851_AddBugReports', N'8.0.0');
GO

COMMIT;
GO

