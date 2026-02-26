using Microsoft.AspNetCore.Identity;
using WFHMonitor.Models;

namespace WFHMonitor.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = ["Admin", "Employee", "Developer"];
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
    }
}
