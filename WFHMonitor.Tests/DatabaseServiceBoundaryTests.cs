namespace WFHMonitor.Tests;

public sealed class DatabaseServiceBoundaryTests
{
    [Fact]
    public void ReactHost_HasNoDatabaseOrIdentityStoreDependencies()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var hostRoot = Path.Combine(repositoryRoot, "WFHMonitor");
        var sourceFiles = Directory
            .EnumerateFiles(hostRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Append(Path.Combine(hostRoot, "WFHMonitor.csproj"));
        var source = string.Join(
            Environment.NewLine,
            sourceFiles.Select(File.ReadAllText));

        Assert.DoesNotContain("ApplicationDbContext", source);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source);
        Assert.DoesNotContain("UseSqlServer", source);
        Assert.DoesNotContain("SaveChanges", source);
        Assert.DoesNotContain("Identity.EntityFrameworkCore", source);
    }

    [Fact]
    public void ApiService_OwnsTheDatabaseContextAndMigrations()
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var apiRoot = Path.Combine(repositoryRoot, "WFHMonitor.ApiService");

        Assert.True(File.Exists(Path.Combine(
            apiRoot,
            "Data",
            "ApplicationDbContext.cs")));
        Assert.True(File.Exists(Path.Combine(
            apiRoot,
            "Migrations",
            "ApplicationDbContextModelSnapshot.cs")));

        var program = File.ReadAllText(Path.Combine(apiRoot, "Program.cs"));
        Assert.Contains("AddDbContextPool<ApplicationDbContext>", program);
        Assert.Contains("db.Database.Migrate()", program);
    }
}
