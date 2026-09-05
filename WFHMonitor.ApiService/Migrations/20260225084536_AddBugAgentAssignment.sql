BEGIN TRANSACTION;
GO

ALTER TABLE [BugReports] ADD [AgentStatus] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [BugReports] ADD [AssigneeType] int NOT NULL DEFAULT 0;
GO

CREATE INDEX [IX_BugReports_AgentStatus] ON [BugReports] ([AgentStatus]);
GO

CREATE INDEX [IX_BugReports_AssigneeType] ON [BugReports] ([AssigneeType]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225084536_AddBugAgentAssignment', N'8.0.0');
GO

COMMIT;
GO

