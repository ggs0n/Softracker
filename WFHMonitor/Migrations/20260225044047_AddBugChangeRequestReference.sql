BEGIN TRANSACTION;
GO

ALTER TABLE [BugReports] ADD [ChangeRequestId] int NULL;
GO

ALTER TABLE [BugReports] ADD [ChangeRequestReferenceText] nvarchar(300) NULL;
GO

CREATE INDEX [IX_BugReports_ChangeRequestId] ON [BugReports] ([ChangeRequestId]);
GO

ALTER TABLE [BugReports] ADD CONSTRAINT [FK_BugReports_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE SET NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225044047_AddBugChangeRequestReference', N'8.0.0');
GO

COMMIT;
GO

