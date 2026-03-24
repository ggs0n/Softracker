using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Models;

namespace WFHMonitor.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services, bool seedDemoUsers = true)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureSchemaColumnsAsync(db);

        string[] roles = ["Admin", "Employee", "Developer", "Tester", "Agent"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (seedDemoUsers)
        {
            if (await userManager.FindByEmailAsync("admin@gmail.com") == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@gmail.com",
                    Email = "admin@gmail.com",
                    FullName = "System Admin",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@1234");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            if (await userManager.FindByEmailAsync("employee@gmail.com") == null)
            {
                var emp = new ApplicationUser
                {
                    UserName = "employee@gmail.com",
                    Email = "employee@gmail.com",
                    FullName = "John Employee",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(emp, "Employee@1234");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(emp, "Employee");
            }

            if (await userManager.FindByEmailAsync("developer@gmail.com") == null)
            {
                var dev = new ApplicationUser
                {
                    UserName = "developer@gmail.com",
                    Email = "developer@gmail.com",
                    FullName = "Jane Developer",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(dev, "Developer@1234");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(dev, "Developer");
            }

            if (await userManager.FindByEmailAsync("agent@gmail.com") == null)
            {
                var agent = new ApplicationUser
                {
                    UserName = "agent@gmail.com",
                    Email = "agent@gmail.com",
                    FullName = "Aiden Agent",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(agent, "Agent@1234");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(agent, "Agent");
            }
        }
    }

    private static async Task EnsureSchemaColumnsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('ChangeRequests', 'GitHubRepoUrl') IS NULL
                ALTER TABLE [ChangeRequests] ADD [GitHubRepoUrl] nvarchar(500) NULL;
            IF COL_LENGTH('ChangeRequests', 'TechnologyStack') IS NULL
                ALTER TABLE [ChangeRequests] ADD [TechnologyStack] nvarchar(800) NULL;
            IF COL_LENGTH('ChangeRequests', 'BugScanStatus') IS NULL
                ALTER TABLE [ChangeRequests] ADD [BugScanStatus] nvarchar(20) NOT NULL CONSTRAINT [DF_ChangeRequests_BugScanStatus] DEFAULT 'None';
            IF COL_LENGTH('ChangeRequests', 'BugScanAgentId') IS NULL
                ALTER TABLE [ChangeRequests] ADD [BugScanAgentId] nvarchar(100) NULL;
            IF COL_LENGTH('ChangeRequests', 'BugScanLastMessage') IS NULL
                ALTER TABLE [ChangeRequests] ADD [BugScanLastMessage] nvarchar(500) NULL;
            IF COL_LENGTH('ChangeRequests', 'BugScanLastRunAt') IS NULL
                ALTER TABLE [ChangeRequests] ADD [BugScanLastRunAt] datetime2 NULL;
            IF COL_LENGTH('ProjectFeatures', 'Status') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectFeatures_Status] DEFAULT 'Draft';
            IF COL_LENGTH('ProjectFeatures', 'Priority') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [Priority] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectFeatures_Priority] DEFAULT 'Medium';
            IF COL_LENGTH('ProjectFeatures', 'Stage') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [Stage] nvarchar(30) NOT NULL CONSTRAINT [DF_ProjectFeatures_Stage] DEFAULT 'Development';
            IF COL_LENGTH('ProjectFeatures', 'TimelineStart') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [TimelineStart] datetime2 NULL;
            IF COL_LENGTH('ProjectFeatures', 'TimelineEnd') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [TimelineEnd] datetime2 NULL;
            IF COL_LENGTH('ProjectFeatures', 'AssignedDeveloperId') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [AssignedDeveloperId] nvarchar(450) NULL;
            IF COL_LENGTH('ProjectFeatures', 'FeatureNumber') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [FeatureNumber] nvarchar(20) NULL;
            IF COL_LENGTH('ProjectFeatures', 'ModuleImpacted') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [ModuleImpacted] nvarchar(200) NULL;
            IF COL_LENGTH('ProjectFeatures', 'LinkedBugs') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [LinkedBugs] nvarchar(1000) NULL;
            IF COL_LENGTH('ProjectFeatures', 'PullRequestUrl') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [PullRequestUrl] nvarchar(500) NULL;
            IF COL_LENGTH('ProjectFeatures', 'AgentStatus') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [AgentStatus] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectFeatures_AgentStatus] DEFAULT 'None';
            IF COL_LENGTH('ProjectFeatures', 'AgentImplementationPlan') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [AgentImplementationPlan] nvarchar(4000) NULL;
            IF COL_LENGTH('ProjectFeatures', 'AgentLastRunAt') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [AgentLastRunAt] datetime2 NULL;
            IF COL_LENGTH('BugReports', 'PullRequestUrl') IS NULL
                ALTER TABLE [BugReports] ADD [PullRequestUrl] nvarchar(500) NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('ProjectFeatures', 'AssignedDeveloperId') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.foreign_keys
                   WHERE name = 'FK_ProjectFeatures_AspNetUsers_AssignedDeveloperId'
               )
            BEGIN
                ALTER TABLE [ProjectFeatures]
                ADD CONSTRAINT [FK_ProjectFeatures_AspNetUsers_AssignedDeveloperId]
                FOREIGN KEY ([AssignedDeveloperId]) REFERENCES [AspNetUsers]([Id]) ON DELETE SET NULL;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('ChangeRequests', 'Stage') IS NOT NULL
            BEGIN
                UPDATE [ChangeRequests]
                SET [Stage] = 1
                WHERE [Stage] = 0;

                UPDATE [ChangeRequests]
                SET [Stage] = 3
                WHERE [Stage] = 4;
            END

            IF COL_LENGTH('ProjectFeatures', 'Stage') IS NOT NULL
            BEGIN
                UPDATE [ProjectFeatures]
                SET [Stage] = 'Development'
                WHERE [Stage] IS NULL
                   OR LTRIM(RTRIM([Stage])) = ''
                   OR [Stage] = 'ProjectStart';

                UPDATE [ProjectFeatures]
                SET [Stage] = 'Deploy'
                WHERE [Stage] = 'DeploymentComplete';
            END

            IF COL_LENGTH('ChangeRequests', 'BugScanStatus') IS NOT NULL
            BEGIN
                UPDATE [ChangeRequests]
                SET [BugScanStatus] = 'None'
                WHERE [BugScanStatus] IS NULL OR LTRIM(RTRIM([BugScanStatus])) = '';
            END

            IF COL_LENGTH('ProjectFeatures', 'ModuleImpacted') IS NOT NULL
            BEGIN
                UPDATE [ProjectFeatures]
                SET [ModuleImpacted] = 'General'
                WHERE [ModuleImpacted] IS NULL OR LTRIM(RTRIM([ModuleImpacted])) = '';
            END

            IF COL_LENGTH('ProjectFeatures', 'LinkedBugs') IS NOT NULL
            BEGIN
                UPDATE [ProjectFeatures]
                SET [LinkedBugs] = '-'
                WHERE [LinkedBugs] IS NULL OR LTRIM(RTRIM([LinkedBugs])) = '';
            END

            IF COL_LENGTH('ProjectFeatures', 'AgentStatus') IS NOT NULL
            BEGIN
                UPDATE [ProjectFeatures]
                SET [AgentStatus] = 'None'
                WHERE [AgentStatus] IS NULL OR LTRIM(RTRIM([AgentStatus])) = '';
            END

            UPDATE c
            SET [CrNumber] = CONCAT('PRJ-', SUBSTRING(c.[CrNumber], 4, 100))
            FROM [ChangeRequests] c
            WHERE c.[CrNumber] LIKE 'CR-%'
              AND NOT EXISTS (
                  SELECT 1
                  FROM [ChangeRequests] c2
                  WHERE c2.[Id] <> c.[Id]
                    AND c2.[CrNumber] = CONCAT('PRJ-', SUBSTRING(c.[CrNumber], 4, 100))
              );

            IF COL_LENGTH('ProjectFeatures', 'FeatureNumber') IS NOT NULL
            BEGIN
                ;WITH Numbered AS
                (
                    SELECT pf.[Id],
                           YEAR(COALESCE(pf.[CreatedAt], SYSUTCDATETIME())) AS [YearPart],
                           ROW_NUMBER() OVER (
                               PARTITION BY YEAR(COALESCE(pf.[CreatedAt], SYSUTCDATETIME()))
                               ORDER BY pf.[CreatedAt], pf.[Id]
                           ) AS [Seq]
                    FROM [ProjectFeatures] pf
                    WHERE pf.[FeatureNumber] IS NULL OR LTRIM(RTRIM(pf.[FeatureNumber])) = ''
                )
                UPDATE pf
                SET [FeatureNumber] = CONCAT(
                    'CR-',
                    CAST(n.[YearPart] AS varchar(4)),
                    '-',
                    RIGHT(CONCAT('0000', CAST(n.[Seq] AS varchar(10))), 4)
                )
                FROM [ProjectFeatures] pf
                INNER JOIN Numbered n ON n.[Id] = pf.[Id];
            END

            IF COL_LENGTH('ProjectFeatures', 'FeatureNumber') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_ProjectFeatures_FeatureNumber'
                     AND object_id = OBJECT_ID(N'[dbo].[ProjectFeatures]')
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_ProjectFeatures_FeatureNumber]
                ON [dbo].[ProjectFeatures]([FeatureNumber])
                WHERE [FeatureNumber] IS NOT NULL;
            END

            IF COL_LENGTH('ProjectFeatures', 'AgentStatus') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_ProjectFeatures_AgentStatus'
                     AND object_id = OBJECT_ID(N'[dbo].[ProjectFeatures]')
               )
            BEGIN
                CREATE INDEX [IX_ProjectFeatures_AgentStatus]
                ON [dbo].[ProjectFeatures]([AgentStatus]);
            END

            IF COL_LENGTH('ChangeRequests', 'BugScanStatus') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_ChangeRequests_BugScanStatus'
                     AND object_id = OBJECT_ID(N'[dbo].[ChangeRequests]')
               )
            BEGIN
                CREATE INDEX [IX_ChangeRequests_BugScanStatus]
                ON [dbo].[ChangeRequests]([BugScanStatus]);
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[RepositoryFeatures]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[RepositoryFeatures]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [ChangeRequestId] int NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_RepositoryFeatures_CreatedAt] DEFAULT SYSUTCDATETIME(),
                    [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_RepositoryFeatures_UpdatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_RepositoryFeatures] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_RepositoryFeatures_ChangeRequests_ChangeRequestId]
                        FOREIGN KEY ([ChangeRequestId]) REFERENCES [dbo].[ChangeRequests]([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[dbo].[RepositoryFeatures]', N'U') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_RepositoryFeatures_ChangeRequestId_Name'
                     AND object_id = OBJECT_ID(N'[dbo].[RepositoryFeatures]')
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_RepositoryFeatures_ChangeRequestId_Name]
                    ON [dbo].[RepositoryFeatures]([ChangeRequestId], [Name]);
            END

            IF OBJECT_ID(N'[dbo].[RepositoryFeatures]', N'U') IS NOT NULL
               AND COL_LENGTH('ProjectFeatures', 'IsAutoDetected') IS NOT NULL
            BEGIN
                INSERT INTO [dbo].[RepositoryFeatures] ([ChangeRequestId], [Name], [Description], [CreatedAt], [UpdatedAt])
                SELECT pf.[ChangeRequestId],
                       pf.[Name],
                       pf.[Description],
                       pf.[CreatedAt],
                       SYSUTCDATETIME()
                FROM [dbo].[ProjectFeatures] pf
                WHERE pf.[IsAutoDetected] = 1
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM [dbo].[RepositoryFeatures] rf
                      WHERE rf.[ChangeRequestId] = pf.[ChangeRequestId]
                        AND rf.[Name] = pf.[Name]
                  );

                DELETE FROM [dbo].[ProjectFeatures]
                WHERE [IsAutoDetected] = 1;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[FeatureScreenshots]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[FeatureScreenshots]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [ProjectFeatureId] int NOT NULL,
                    [FileName] nvarchar(300) NOT NULL,
                    [OriginalFileName] nvarchar(300) NOT NULL,
                    [UploadedAt] datetime2 NOT NULL CONSTRAINT [DF_FeatureScreenshots_UploadedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_FeatureScreenshots] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_FeatureScreenshots_ProjectFeatures_ProjectFeatureId]
                        FOREIGN KEY ([ProjectFeatureId]) REFERENCES [dbo].[ProjectFeatures]([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[dbo].[FeatureScreenshots]', N'U') IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_FeatureScreenshots_ProjectFeatureId'
                     AND object_id = OBJECT_ID(N'[dbo].[FeatureScreenshots]')
               )
            BEGIN
                CREATE INDEX [IX_FeatureScreenshots_ProjectFeatureId]
                    ON [dbo].[FeatureScreenshots]([ProjectFeatureId]);
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('AspNetUsers', 'SubscriptionPlan') IS NULL
                ALTER TABLE [AspNetUsers] ADD [SubscriptionPlan] nvarchar(20) NOT NULL CONSTRAINT [DF_AspNetUsers_SubscriptionPlan] DEFAULT 'Free';
            IF COL_LENGTH('AspNetUsers', 'CompanyName') IS NULL
                ALTER TABLE [AspNetUsers] ADD [CompanyName] nvarchar(200) NULL;
            IF COL_LENGTH('AspNetUsers', 'OrganizationTeam') IS NULL
                ALTER TABLE [AspNetUsers] ADD [OrganizationTeam] nvarchar(20) NOT NULL CONSTRAINT [DF_AspNetUsers_OrganizationTeam] DEFAULT 'Unassigned';
            IF COL_LENGTH('AspNetUsers', 'IsProSubscriptionActive') IS NULL
                ALTER TABLE [AspNetUsers] ADD [IsProSubscriptionActive] bit NOT NULL CONSTRAINT [DF_AspNetUsers_IsProSubscriptionActive] DEFAULT 0;
            IF COL_LENGTH('AspNetUsers', 'ProSubscribedAt') IS NULL
                ALTER TABLE [AspNetUsers] ADD [ProSubscribedAt] datetime2 NULL;
            IF COL_LENGTH('AspNetUsers', 'ProSubscriptionEndsAt') IS NULL
                ALTER TABLE [AspNetUsers] ADD [ProSubscriptionEndsAt] datetime2 NULL;
            IF COL_LENGTH('AspNetUsers', 'IsProCancelAtPeriodEnd') IS NULL
                ALTER TABLE [AspNetUsers] ADD [IsProCancelAtPeriodEnd] bit NOT NULL CONSTRAINT [DF_AspNetUsers_IsProCancelAtPeriodEnd] DEFAULT 0;
            IF COL_LENGTH('AspNetUsers', 'StripeCustomerId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [StripeCustomerId] nvarchar(100) NULL;
            IF COL_LENGTH('AspNetUsers', 'StripeSubscriptionId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [StripeSubscriptionId] nvarchar(100) NULL;
            IF COL_LENGTH('AspNetUsers', 'LastProcessedStripeCheckoutSessionId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [LastProcessedStripeCheckoutSessionId] nvarchar(200) NULL;
            IF COL_LENGTH('AspNetUsers', 'PendingStripeCheckoutSessionId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [PendingStripeCheckoutSessionId] nvarchar(200) NULL;
            IF COL_LENGTH('AspNetUsers', 'PendingStripeCheckoutUrl') IS NULL
                ALTER TABLE [AspNetUsers] ADD [PendingStripeCheckoutUrl] nvarchar(1000) NULL;
            IF COL_LENGTH('AspNetUsers', 'PendingStripeCheckoutCreatedAt') IS NULL
                ALTER TABLE [AspNetUsers] ADD [PendingStripeCheckoutCreatedAt] datetime2 NULL;
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('AspNetUsers', 'SubscriptionPlan') IS NOT NULL
            BEGIN
            UPDATE [AspNetUsers]
            SET [SubscriptionPlan] = 'Free'
            WHERE [SubscriptionPlan] IS NULL OR LTRIM(RTRIM([SubscriptionPlan])) = '';
            END

            IF COL_LENGTH('AspNetUsers', 'OrganizationTeam') IS NOT NULL
            BEGIN
            UPDATE [AspNetUsers]
            SET [OrganizationTeam] = 'Unassigned'
            WHERE [OrganizationTeam] IS NULL OR LTRIM(RTRIM([OrganizationTeam])) = '';
            END

            IF COL_LENGTH('AspNetUsers', 'ProSubscriptionEndsAt') IS NOT NULL
            BEGIN
            UPDATE [AspNetUsers]
            SET [ProSubscriptionEndsAt] = DATEADD(month, 1, [ProSubscribedAt])
            WHERE [IsProSubscriptionActive] = 1
              AND [ProSubscribedAt] IS NOT NULL
              AND [ProSubscriptionEndsAt] IS NULL;
            END

            IF COL_LENGTH('AspNetUsers', 'FullName') IS NOT NULL
               AND COL_LENGTH('AspNetUsers', 'Email') IS NOT NULL
            BEGIN
                UPDATE [AspNetUsers]
                SET [FullName] = REPLACE(REPLACE(REPLACE(LEFT([Email], CHARINDEX('@', [Email]) - 1), '.', ' '), '_', ' '), '-', ' ')
                WHERE [FullName] = [Email]
                  AND [Email] IS NOT NULL
                  AND CHARINDEX('@', [Email]) > 1;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[OrgTeams]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[OrgTeams]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [CompanyName] nvarchar(200) NULL,
                    [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_OrgTeams_CreatedAt] DEFAULT SYSUTCDATETIME(),
                    [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_OrgTeams_UpdatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_OrgTeams] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[OrgTeams]', N'U') IS NOT NULL
               AND COL_LENGTH('OrgTeams', 'CompanyName') IS NULL
            BEGIN
                ALTER TABLE [dbo].[OrgTeams]
                    ADD [CompanyName] nvarchar(200) NULL;
            END

            IF OBJECT_ID(N'[dbo].[OrgTeams]', N'U') IS NOT NULL
               AND EXISTS
               (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_OrgTeams_Name'
                     AND object_id = OBJECT_ID(N'[dbo].[OrgTeams]')
               )
            BEGIN
                DROP INDEX [IX_OrgTeams_Name] ON [dbo].[OrgTeams];
            END

            IF OBJECT_ID(N'[dbo].[OrgTeams]', N'U') IS NOT NULL
               AND COL_LENGTH('OrgTeams', 'CompanyName') IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_OrgTeams_CompanyName_Name'
                     AND object_id = OBJECT_ID(N'[dbo].[OrgTeams]')
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_OrgTeams_CompanyName_Name]
                    ON [dbo].[OrgTeams]([CompanyName], [Name]);
            END

            IF COL_LENGTH('AspNetUsers', 'OrgTeamId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [OrgTeamId] int NULL;

            IF COL_LENGTH('ChangeRequests', 'OrgTeamId') IS NULL
                ALTER TABLE [ChangeRequests] ADD [OrgTeamId] int NULL;

            IF COL_LENGTH('AspNetUsers', 'OrgTeamId') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[OrgTeams]', N'U') IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM sys.foreign_keys
                   WHERE name = 'FK_AspNetUsers_OrgTeams_OrgTeamId'
               )
            BEGIN
                ALTER TABLE [AspNetUsers]
                ADD CONSTRAINT [FK_AspNetUsers_OrgTeams_OrgTeamId]
                    FOREIGN KEY ([OrgTeamId]) REFERENCES [dbo].[OrgTeams]([Id]) ON DELETE SET NULL;
            END

            IF COL_LENGTH('ChangeRequests', 'OrgTeamId') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[OrgTeams]', N'U') IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM sys.foreign_keys
                   WHERE name = 'FK_ChangeRequests_OrgTeams_OrgTeamId'
               )
            BEGIN
                ALTER TABLE [ChangeRequests]
                ADD CONSTRAINT [FK_ChangeRequests_OrgTeams_OrgTeamId]
                    FOREIGN KEY ([OrgTeamId]) REFERENCES [dbo].[OrgTeams]([Id]) ON DELETE SET NULL;
            END

            IF COL_LENGTH('AspNetUsers', 'LastActivityAt') IS NULL
                ALTER TABLE [AspNetUsers] ADD [LastActivityAt] datetime2 NULL;

            IF OBJECT_ID(N'[dbo].[OrganizationProfiles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[OrganizationProfiles]
                (
                    [Id] int NOT NULL,
                    [CeoUserId] nvarchar(450) NULL,
                    [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_OrganizationProfiles_UpdatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_OrganizationProfiles] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[OrganizationProfiles]', N'U') IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM sys.foreign_keys
                   WHERE name = 'FK_OrganizationProfiles_AspNetUsers_CeoUserId'
               )
            BEGIN
                ALTER TABLE [OrganizationProfiles]
                ADD CONSTRAINT [FK_OrganizationProfiles_AspNetUsers_CeoUserId]
                    FOREIGN KEY ([CeoUserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE SET NULL;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('OrgTeams', 'CompanyName') IS NOT NULL
            BEGIN
                UPDATE t
                SET [CompanyName] = src.[CompanyName]
                FROM [dbo].[OrgTeams] t
                CROSS APPLY
                (
                    SELECT TOP 1 u.[CompanyName]
                    FROM [dbo].[AspNetUsers] u
                    WHERE u.[OrgTeamId] = t.[Id]
                      AND u.[CompanyName] IS NOT NULL
                      AND LTRIM(RTRIM(u.[CompanyName])) <> ''
                    ORDER BY u.[Id]
                ) src
                WHERE t.[CompanyName] IS NULL
                   OR LTRIM(RTRIM(t.[CompanyName])) = '';

                UPDATE t
                SET [CompanyName] = src.[CompanyName]
                FROM [dbo].[OrgTeams] t
                CROSS APPLY
                (
                    SELECT TOP 1 u.[CompanyName]
                    FROM [dbo].[ChangeRequests] c
                    INNER JOIN [dbo].[AspNetUsers] u ON u.[Id] = c.[CreatedById]
                    WHERE c.[OrgTeamId] = t.[Id]
                      AND u.[CompanyName] IS NOT NULL
                      AND LTRIM(RTRIM(u.[CompanyName])) <> ''
                    ORDER BY c.[Id]
                ) src
                WHERE (t.[CompanyName] IS NULL OR LTRIM(RTRIM(t.[CompanyName])) = '');
            END

            DECLARE @Team1Id int = (SELECT TOP 1 [Id] FROM [dbo].[OrgTeams] WHERE [Name] = 'Team 1' ORDER BY [Id]);
            DECLARE @Team2Id int = (SELECT TOP 1 [Id] FROM [dbo].[OrgTeams] WHERE [Name] = 'Team 2' ORDER BY [Id]);

            IF COL_LENGTH('AspNetUsers', 'OrgTeamId') IS NOT NULL
               AND COL_LENGTH('AspNetUsers', 'OrganizationTeam') IS NOT NULL
            BEGIN
                UPDATE [AspNetUsers]
                SET [OrgTeamId] = @Team1Id
                WHERE [OrgTeamId] IS NULL
                  AND [OrganizationTeam] = 'Team1'
                  AND @Team1Id IS NOT NULL;

                UPDATE [AspNetUsers]
                SET [OrgTeamId] = @Team2Id
                WHERE [OrgTeamId] IS NULL
                  AND [OrganizationTeam] = 'Team2'
                  AND @Team2Id IS NOT NULL;
            END

            IF COL_LENGTH('ChangeRequests', 'OrgTeamId') IS NOT NULL
               AND COL_LENGTH('AspNetUsers', 'OrgTeamId') IS NOT NULL
            BEGIN
                UPDATE cr
                SET [OrgTeamId] = u.[OrgTeamId]
                FROM [ChangeRequests] cr
                INNER JOIN [AspNetUsers] u ON u.[Id] = cr.[CreatedById]
                WHERE cr.[OrgTeamId] IS NULL
                  AND u.[OrgTeamId] IS NOT NULL;
            END

            IF OBJECT_ID(N'[dbo].[OrganizationProfiles]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[OrganizationProfiles] WHERE [Id] = 1)
            BEGIN
                INSERT INTO [dbo].[OrganizationProfiles] ([Id], [CeoUserId], [UpdatedAt])
                VALUES
                (
                    1,
                    (SELECT TOP 1 [Id] FROM [dbo].[AspNetUsers] WHERE [Email] = 'admin@gmail.com' ORDER BY [Id]),
                    SYSUTCDATETIME()
                );
            END

            IF OBJECT_ID(N'[dbo].[OrganizationProfiles]', N'U') IS NOT NULL
            BEGIN
                UPDATE [dbo].[OrganizationProfiles]
                SET [CeoUserId] = (SELECT TOP 1 [Id] FROM [dbo].[AspNetUsers] WHERE [Email] = 'admin@gmail.com' ORDER BY [Id]),
                    [UpdatedAt] = SYSUTCDATETIME()
                WHERE [Id] = 1
                  AND [CeoUserId] IS NULL;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[ModulePermissionSettings]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ModulePermissionSettings]
                (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [ModuleKey] nvarchar(50) NOT NULL,
                    [ViewRolesCsv] nvarchar(400) NOT NULL,
                    [ModifyRolesCsv] nvarchar(400) NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_ModulePermissionSettings_UpdatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_ModulePermissionSettings] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID(N'[dbo].[ModulePermissionSettings]', N'U') IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM sys.indexes
                   WHERE name = 'IX_ModulePermissionSettings_ModuleKey'
                     AND object_id = OBJECT_ID(N'[dbo].[ModulePermissionSettings]')
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_ModulePermissionSettings_ModuleKey]
                    ON [dbo].[ModulePermissionSettings]([ModuleKey]);
            END

            IF OBJECT_ID(N'[dbo].[ModulePermissionSettings]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [dbo].[ModulePermissionSettings] WHERE [ModuleKey] = 'AllProjects')
                BEGIN
                    INSERT INTO [dbo].[ModulePermissionSettings] ([ModuleKey], [ViewRolesCsv], [ModifyRolesCsv], [UpdatedAt])
                    VALUES ('AllProjects', 'Admin,Tester,Developer,Agent,Employee', 'Admin', SYSUTCDATETIME());
                END

                IF NOT EXISTS (SELECT 1 FROM [dbo].[ModulePermissionSettings] WHERE [ModuleKey] = 'Features')
                BEGIN
                    INSERT INTO [dbo].[ModulePermissionSettings] ([ModuleKey], [ViewRolesCsv], [ModifyRolesCsv], [UpdatedAt])
                    VALUES ('Features', 'Admin,Tester,Developer,Agent,Employee', 'Admin,Developer', SYSUTCDATETIME());
                END

                IF NOT EXISTS (SELECT 1 FROM [dbo].[ModulePermissionSettings] WHERE [ModuleKey] = 'Bugs')
                BEGIN
                    INSERT INTO [dbo].[ModulePermissionSettings] ([ModuleKey], [ViewRolesCsv], [ModifyRolesCsv], [UpdatedAt])
                    VALUES ('Bugs', 'Admin,Tester,Developer,Agent', 'Admin,Tester,Developer,Agent', SYSUTCDATETIME());
                END

                IF NOT EXISTS (SELECT 1 FROM [dbo].[ModulePermissionSettings] WHERE [ModuleKey] = 'QaTesting')
                BEGIN
                    INSERT INTO [dbo].[ModulePermissionSettings] ([ModuleKey], [ViewRolesCsv], [ModifyRolesCsv], [UpdatedAt])
                    VALUES ('QaTesting', 'Admin,Tester,Developer', 'Admin,Tester', SYSUTCDATETIME());
                END
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SystemPreferences]
                (
                    [Id] int NOT NULL,
                    [BellNotificationSoundEnabled] bit NOT NULL CONSTRAINT [DF_SystemPreferences_BellNotificationSoundEnabled] DEFAULT 1,
                    [BellNotificationSoundOption] nvarchar(30) NOT NULL CONSTRAINT [DF_SystemPreferences_BellNotificationSoundOption] DEFAULT 'classic',
                    [FreeProjectLimit] int NOT NULL CONSTRAINT [DF_SystemPreferences_FreeProjectLimit] DEFAULT 2,
                    [FreeBugLimit] int NOT NULL CONSTRAINT [DF_SystemPreferences_FreeBugLimit] DEFAULT 2,
                    [FreeFeatureLimit] int NOT NULL CONSTRAINT [DF_SystemPreferences_FreeFeatureLimit] DEFAULT 2,
                    [EnableOpenClawAgents] bit NOT NULL CONSTRAINT [DF_SystemPreferences_EnableOpenClawAgents] DEFAULT 1,
                    [AllowOpenClawForFreePlan] bit NOT NULL CONSTRAINT [DF_SystemPreferences_AllowOpenClawForFreePlan] DEFAULT 0,
                    [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_SystemPreferences_UpdatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_SystemPreferences] PRIMARY KEY ([Id])
                );
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'BellNotificationSoundOption') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SystemPreferences]
                    ADD [BellNotificationSoundOption] nvarchar(30) NOT NULL
                    CONSTRAINT [DF_SystemPreferences_BellNotificationSoundOption] DEFAULT 'classic';
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'FreeProjectLimit') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SystemPreferences]
                    ADD [FreeProjectLimit] int NOT NULL
                    CONSTRAINT [DF_SystemPreferences_FreeProjectLimit] DEFAULT 2;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'FreeBugLimit') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SystemPreferences]
                    ADD [FreeBugLimit] int NOT NULL
                    CONSTRAINT [DF_SystemPreferences_FreeBugLimit] DEFAULT 2;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'FreeFeatureLimit') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SystemPreferences]
                    ADD [FreeFeatureLimit] int NOT NULL
                    CONSTRAINT [DF_SystemPreferences_FreeFeatureLimit] DEFAULT 2;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'EnableOpenClawAgents') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SystemPreferences]
                    ADD [EnableOpenClawAgents] bit NOT NULL
                    CONSTRAINT [DF_SystemPreferences_EnableOpenClawAgents] DEFAULT 1;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'AllowOpenClawForFreePlan') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SystemPreferences]
                    ADD [AllowOpenClawForFreePlan] bit NOT NULL
                    CONSTRAINT [DF_SystemPreferences_AllowOpenClawForFreePlan] DEFAULT 0;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[SystemPreferences] WHERE [Id] = 1)
            BEGIN
                INSERT INTO [dbo].[SystemPreferences]
                    ([Id], [BellNotificationSoundEnabled], [BellNotificationSoundOption], [FreeProjectLimit], [FreeBugLimit], [FreeFeatureLimit], [EnableOpenClawAgents], [AllowOpenClawForFreePlan], [UpdatedAt])
                VALUES
                    (1, 1, 'classic', 2, 2, 2, 1, 0, SYSUTCDATETIME());
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
               AND COL_LENGTH('SystemPreferences', 'BellNotificationSoundOption') IS NOT NULL
            BEGIN
                UPDATE [dbo].[SystemPreferences]
                SET [BellNotificationSoundOption] = 'classic'
                WHERE [BellNotificationSoundOption] IS NULL
                   OR LTRIM(RTRIM([BellNotificationSoundOption])) = '';
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[SystemPreferences]', N'U') IS NOT NULL
            BEGIN
                UPDATE [dbo].[SystemPreferences]
                SET
                    [FreeProjectLimit] = CASE WHEN [FreeProjectLimit] < 0 THEN 2 ELSE [FreeProjectLimit] END,
                    [FreeBugLimit] = CASE WHEN [FreeBugLimit] < 0 THEN 2 ELSE [FreeBugLimit] END,
                    [FreeFeatureLimit] = CASE WHEN [FreeFeatureLimit] < 0 THEN 2 ELSE [FreeFeatureLimit] END
                WHERE [FreeProjectLimit] < 0
                   OR [FreeBugLimit] < 0
                   OR [FreeFeatureLimit] < 0;
            END
            """);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[UserOnboardingStates]
                (
                    [UserId] nvarchar(450) NOT NULL,
                    [IsDismissed] bit NOT NULL CONSTRAINT [DF_UserOnboardingStates_IsDismissed] DEFAULT 0,
                    [IsCompleted] bit NOT NULL CONSTRAINT [DF_UserOnboardingStates_IsCompleted] DEFAULT 0,
                    [LastSeenStepKey] nvarchar(50) NOT NULL CONSTRAINT [DF_UserOnboardingStates_LastSeenStepKey] DEFAULT 'add-project',
                    [DismissedAt] datetime2 NULL,
                    [CompletedAt] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_UserOnboardingStates_CreatedAt] DEFAULT SYSUTCDATETIME(),
                    [UpdatedAt] datetime2 NOT NULL CONSTRAINT [DF_UserOnboardingStates_UpdatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_UserOnboardingStates] PRIMARY KEY ([UserId]),
                    CONSTRAINT [FK_UserOnboardingStates_AspNetUsers_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NOT NULL
               AND COL_LENGTH('UserOnboardingStates', 'LastSeenStepKey') IS NULL
            BEGIN
                ALTER TABLE [dbo].[UserOnboardingStates]
                    ADD [LastSeenStepKey] nvarchar(50) NOT NULL
                    CONSTRAINT [DF_UserOnboardingStates_LastSeenStepKey] DEFAULT 'add-project';
            END

            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NOT NULL
               AND COL_LENGTH('UserOnboardingStates', 'DismissedAt') IS NULL
            BEGIN
                ALTER TABLE [dbo].[UserOnboardingStates]
                    ADD [DismissedAt] datetime2 NULL;
            END

            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NOT NULL
               AND COL_LENGTH('UserOnboardingStates', 'CompletedAt') IS NULL
            BEGIN
                ALTER TABLE [dbo].[UserOnboardingStates]
                    ADD [CompletedAt] datetime2 NULL;
            END

            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NOT NULL
               AND COL_LENGTH('UserOnboardingStates', 'CreatedAt') IS NULL
            BEGIN
                ALTER TABLE [dbo].[UserOnboardingStates]
                    ADD [CreatedAt] datetime2 NOT NULL
                    CONSTRAINT [DF_UserOnboardingStates_CreatedAt] DEFAULT SYSUTCDATETIME();
            END

            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NOT NULL
               AND COL_LENGTH('UserOnboardingStates', 'UpdatedAt') IS NULL
            BEGIN
                ALTER TABLE [dbo].[UserOnboardingStates]
                    ADD [UpdatedAt] datetime2 NOT NULL
                    CONSTRAINT [DF_UserOnboardingStates_UpdatedAt] DEFAULT SYSUTCDATETIME();
            END

            IF OBJECT_ID(N'[dbo].[UserOnboardingStates]', N'U') IS NOT NULL
               AND COL_LENGTH('UserOnboardingStates', 'LastSeenStepKey') IS NOT NULL
            BEGIN
                UPDATE [dbo].[UserOnboardingStates]
                SET [LastSeenStepKey] = 'add-project'
                WHERE [LastSeenStepKey] IS NULL
                   OR LTRIM(RTRIM([LastSeenStepKey])) = '';
            END
            """);
    }
}
