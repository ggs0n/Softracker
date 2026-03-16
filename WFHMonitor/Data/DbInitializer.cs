using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Models;

namespace WFHMonitor.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
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

    private static async Task EnsureSchemaColumnsAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('ChangeRequests', 'GitHubRepoUrl') IS NULL
                ALTER TABLE [ChangeRequests] ADD [GitHubRepoUrl] nvarchar(500) NULL;
            IF COL_LENGTH('ChangeRequests', 'TechnologyStack') IS NULL
                ALTER TABLE [ChangeRequests] ADD [TechnologyStack] nvarchar(800) NULL;
            IF COL_LENGTH('ProjectFeatures', 'Status') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectFeatures_Status] DEFAULT 'Draft';
            IF COL_LENGTH('ProjectFeatures', 'Priority') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [Priority] nvarchar(20) NOT NULL CONSTRAINT [DF_ProjectFeatures_Priority] DEFAULT 'Medium';
            IF COL_LENGTH('ProjectFeatures', 'Stage') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [Stage] nvarchar(30) NOT NULL CONSTRAINT [DF_ProjectFeatures_Stage] DEFAULT 'ProjectStart';
            IF COL_LENGTH('ProjectFeatures', 'TimelineStart') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [TimelineStart] datetime2 NULL;
            IF COL_LENGTH('ProjectFeatures', 'TimelineEnd') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [TimelineEnd] datetime2 NULL;
            IF COL_LENGTH('ProjectFeatures', 'AssignedDeveloperId') IS NULL
                ALTER TABLE [ProjectFeatures] ADD [AssignedDeveloperId] nvarchar(450) NULL;
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
            IF COL_LENGTH('AspNetUsers', 'SubscriptionPlan') IS NULL
                ALTER TABLE [AspNetUsers] ADD [SubscriptionPlan] nvarchar(20) NOT NULL CONSTRAINT [DF_AspNetUsers_SubscriptionPlan] DEFAULT 'Free';
            IF COL_LENGTH('AspNetUsers', 'OrganizationTeam') IS NULL
                ALTER TABLE [AspNetUsers] ADD [OrganizationTeam] nvarchar(20) NOT NULL CONSTRAINT [DF_AspNetUsers_OrganizationTeam] DEFAULT 'Unassigned';
            IF COL_LENGTH('AspNetUsers', 'IsProSubscriptionActive') IS NULL
                ALTER TABLE [AspNetUsers] ADD [IsProSubscriptionActive] bit NOT NULL CONSTRAINT [DF_AspNetUsers_IsProSubscriptionActive] DEFAULT 0;
            IF COL_LENGTH('AspNetUsers', 'ProSubscribedAt') IS NULL
                ALTER TABLE [AspNetUsers] ADD [ProSubscribedAt] datetime2 NULL;
            IF COL_LENGTH('AspNetUsers', 'StripeCustomerId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [StripeCustomerId] nvarchar(100) NULL;
            IF COL_LENGTH('AspNetUsers', 'StripeSubscriptionId') IS NULL
                ALTER TABLE [AspNetUsers] ADD [StripeSubscriptionId] nvarchar(100) NULL;
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
            """);
    }
}
