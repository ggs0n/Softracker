namespace WFHMonitor.Services;

public static class FeatureDetector
{
    private static readonly (string Name, string[] Patterns)[] FeaturePatterns =
    {
        ("Login / Authentication", new[] { "login", "signin", "sign-in", "auth", "authenticate" }),
        ("Registration / Sign Up", new[] { "register", "signup", "sign-up" }),
        ("Logout", new[] { "logout", "signout", "sign-out" }),
        ("Shopping Cart", new[] { "cart", "basket", "shopping-cart" }),
        ("Checkout / Payment", new[] { "checkout", "payment", "stripe", "paypal", "billing" }),
        ("Search", new[] { "search" }),
        ("User Profile", new[] { "profile", "account", "my-account" }),
        ("Dashboard", new[] { "dashboard" }),
        ("Admin Panel", new[] { "admin", "backoffice", "back-office" }),
        ("Notifications", new[] { "notification", "notifications" }),
        ("File Upload", new[] { "upload", "file-upload", "fileupload" }),
        ("API / REST", new[] { "api", "swagger", "openapi" }),
        ("Email", new[] { "email", "smtp", "mailer", "sendgrid", "mailgun" }),
        ("Chat / Messaging", new[] { "chat", "messaging", "realtime" }),
        ("Reports / Analytics", new[] { "report", "reports", "analytics", "chart", "charts" }),
        ("Calendar / Scheduling", new[] { "calendar", "schedule", "scheduling", "event" }),
        ("Roles / Permissions", new[] { "role", "roles", "permission", "permissions", "authorize" }),
        ("Database / ORM", new[] { "migration", "migrations", "dbcontext", "entity" }),
        ("Testing", new[] { "test", "tests", "spec", "specs", "xunit", "nunit", "jest" }),
        ("Logging", new[] { "serilog", "nlog", "logging", "logger" }),
    };

    public static List<(string Name, string Description)> DetectFeatures(List<string> filePaths)
    {
        var detected = new List<(string Name, string Description)>();

        foreach (var (name, patterns) in FeaturePatterns)
        {
            var matchingFiles = filePaths
                .Where(path => patterns.Any(p =>
                    path.Split('/', '\\')
                        .Any(segment => segment.Contains(p, StringComparison.OrdinalIgnoreCase))))
                .Take(5)
                .ToList();

            if (matchingFiles.Count > 0)
            {
                var description = $"Detected in: {string.Join(", ", matchingFiles.Select(f => f.Split('/').Last()))}";
                detected.Add((name, description));
            }
        }

        return detected;
    }
}
