IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [EodReports] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] nvarchar(450) NOT NULL,
    [ReportDate] datetime2 NOT NULL,
    [SubmittedAt] datetime2 NOT NULL,
    [Blockers] nvarchar(2000) NULL,
    [TomorrowPlan] nvarchar(2000) NULL,
    [EvidenceLinks] nvarchar(2000) NULL,
    CONSTRAINT [PK_EodReports] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EodReports_AspNetUsers_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorkTasks] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [Status] int NOT NULL,
    [DueDate] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [AssigneeId] nvarchar(450) NULL,
    [CreatedById] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_WorkTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkTasks_AspNetUsers_AssigneeId] FOREIGN KEY ([AssigneeId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_WorkTasks_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [EodReportTasks] (
    [Id] int NOT NULL IDENTITY,
    [EodReportId] int NOT NULL,
    [TaskId] int NOT NULL,
    [Type] int NOT NULL,
    [NextStep] nvarchar(500) NULL,
    CONSTRAINT [PK_EodReportTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EodReportTasks_EodReports_EodReportId] FOREIGN KEY ([EodReportId]) REFERENCES [EodReports] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EodReportTasks_WorkTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_EodReports_EmployeeId] ON [EodReports] ([EmployeeId]);
GO

CREATE UNIQUE INDEX [IX_EodReports_EmployeeId_ReportDate] ON [EodReports] ([EmployeeId], [ReportDate]);
GO

CREATE INDEX [IX_EodReports_ReportDate] ON [EodReports] ([ReportDate]);
GO

CREATE INDEX [IX_EodReportTasks_EodReportId] ON [EodReportTasks] ([EodReportId]);
GO

CREATE INDEX [IX_EodReportTasks_TaskId] ON [EodReportTasks] ([TaskId]);
GO

CREATE INDEX [IX_WorkTasks_AssigneeId] ON [WorkTasks] ([AssigneeId]);
GO

CREATE INDEX [IX_WorkTasks_CreatedById] ON [WorkTasks] ([CreatedById]);
GO

CREATE INDEX [IX_WorkTasks_DueDate] ON [WorkTasks] ([DueDate]);
GO

CREATE INDEX [IX_WorkTasks_Status] ON [WorkTasks] ([Status]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260224234624_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ChangeRequests] (
    [Id] int NOT NULL IDENTITY,
    [CrNumber] nvarchar(20) NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [Status] int NOT NULL,
    [Priority] int NOT NULL,
    [FigmaLink] nvarchar(500) NULL,
    [ArchSpecLink] nvarchar(500) NULL,
    [ArchSpecNotes] nvarchar(2000) NULL,
    [TimelineStart] datetime2 NULL,
    [TimelineEnd] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [CreatedById] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_ChangeRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChangeRequests_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ChangeRequestPics] (
    [Id] int NOT NULL IDENTITY,
    [ChangeRequestId] int NOT NULL,
    [EmployeeId] nvarchar(450) NOT NULL,
    [Role] nvarchar(100) NULL,
    CONSTRAINT [PK_ChangeRequestPics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ChangeRequestPics_AspNetUsers_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ChangeRequestPics_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ChangeRequestPics_ChangeRequestId] ON [ChangeRequestPics] ([ChangeRequestId]);
GO

CREATE INDEX [IX_ChangeRequestPics_EmployeeId] ON [ChangeRequestPics] ([EmployeeId]);
GO

CREATE INDEX [IX_ChangeRequests_CreatedById] ON [ChangeRequests] ([CreatedById]);
GO

CREATE UNIQUE INDEX [IX_ChangeRequests_CrNumber] ON [ChangeRequests] ([CrNumber]);
GO

CREATE INDEX [IX_ChangeRequests_Status] ON [ChangeRequests] ([Status]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225002241_AddChangeRequests', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ChangeRequests] ADD [GitHubBranch] nvarchar(100) NULL;
GO

ALTER TABLE [ChangeRequests] ADD [GitHubRepoName] nvarchar(100) NULL;
GO

ALTER TABLE [ChangeRequests] ADD [GitHubRepoOwner] nvarchar(100) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225005910_AddGitHubFields', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ArchSpecImages] (
    [Id] int NOT NULL IDENTITY,
    [ChangeRequestId] int NOT NULL,
    [FileName] nvarchar(300) NOT NULL,
    [Caption] nvarchar(1000) NULL,
    [SortOrder] int NOT NULL,
    [UploadedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ArchSpecImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ArchSpecImages_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ArchSpecImages_ChangeRequestId] ON [ArchSpecImages] ([ChangeRequestId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260225013201_AddArchSpecImages', N'8.0.0');
GO

COMMIT;
GO

