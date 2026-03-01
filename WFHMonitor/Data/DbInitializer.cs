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
            """);
    }
}
