BEGIN TRANSACTION;
GO

IF COL_LENGTH('ChangeRequests', 'GitHubRepoUrl') IS NULL
    ALTER TABLE [ChangeRequests] ADD [GitHubRepoUrl] nvarchar(500) NULL;
GO

COMMIT;
GO
