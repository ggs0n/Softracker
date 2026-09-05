using System.Text.RegularExpressions;

namespace WFHMonitor.Services;

public sealed record DetectedRepositoryModule(
    string Name,
    string Description,
    IReadOnlyList<string> EvidencePaths);

public static class FeatureDetector
{
    private static readonly (string Name, string[] Patterns)[] FeaturePatterns =
    {
        ("Login / Authentication", ["login", "signin", "sign-in", "auth", "authenticate"]),
        ("Registration / Sign Up", ["register", "signup", "sign-up"]),
        ("Logout", ["logout", "signout", "sign-out"]),
        ("Shopping Cart", ["cart", "basket", "shopping-cart"]),
        ("Checkout / Payment", ["checkout", "payment", "stripe", "paypal", "billing"]),
        ("Search", ["search"]),
        ("User Profile", ["profile", "account", "my-account"]),
        ("Dashboard", ["dashboard"]),
        ("Admin Panel", ["admin", "backoffice", "back-office"]),
        ("Notifications", ["notification", "notifications"]),
        ("File Upload", ["upload", "file-upload", "fileupload"]),
        ("API / REST", ["api", "swagger", "openapi"]),
        ("Email", ["email", "smtp", "mailer", "sendgrid", "mailgun"]),
        ("Chat / Messaging", ["chat", "messaging", "realtime"]),
        ("Reports / Analytics", ["report", "reports", "analytics", "chart", "charts"]),
        ("Calendar / Scheduling", ["calendar", "schedule", "scheduling", "event"]),
        ("Roles / Permissions", ["role", "roles", "permission", "permissions", "authorize"]),
        ("Database / ORM", ["migration", "migrations", "dbcontext", "entity"]),
        ("Testing", ["test", "tests", "spec", "specs", "xunit", "nunit", "jest"]),
        ("Logging", ["serilog", "nlog", "logging", "logger"])
    };

    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "app", "application", "base", "common", "component", "components", "controller", "controllers",
        "data", "default", "error", "footer", "header", "home", "homepage", "index", "main", "model",
        "models", "navbar", "page", "pages", "program", "repository", "request", "response", "service",
        "services", "shared", "startup", "utility", "utils", "weather forecast", "values",
        "sample", "template", "layout", "view imports", "view start", "global usings", "add", "create",
        "delete", "details", "edit", "list", "new", "privacy", "remove", "update"
    };

    private static readonly HashSet<string> ModuleBearingDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "areas", "component", "components", "controller", "controllers", "domain", "entities",
        "entity", "feature", "features", "handler", "handlers", "model", "models", "module", "modules",
        "page", "pages", "repository", "repositories", "service", "services", "view", "viewmodel",
        "viewmodels", "views"
    };

    private static readonly HashSet<string> CandidateExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cshtml", ".razor", ".js", ".jsx", ".ts", ".tsx", ".java", ".py", ".go", ".rb", ".php"
    };

    public static List<(string Name, string Description)> DetectFeatures(List<string> filePaths)
    {
        var detected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, patterns) in FeaturePatterns)
        {
            var matchingFiles = filePaths
                .Where(path => PathMatchesAny(path, patterns))
                .Take(5)
                .ToList();

            if (matchingFiles.Count > 0)
            {
                detected[name] = matchingFiles.Count == 1
                    ? $"Auto-detected from {matchingFiles[0]}."
                    : $"Auto-detected from {matchingFiles.Count} repository paths.";
            }
        }

        foreach (var module in DetectModules(filePaths))
            detected.TryAdd(module.Name, module.Description);

        return detected.Select(item => (item.Key, item.Value)).ToList();
    }

    public static List<DetectedRepositoryModule> DetectModules(IEnumerable<string> filePaths)
    {
        var evidenceByModule = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        void AddEvidence(string moduleName, string path)
        {
            moduleName = NormalizeModuleName(moduleName);
            if (string.IsNullOrWhiteSpace(moduleName) || GenericNames.Contains(moduleName))
                return;

            if (!evidenceByModule.TryGetValue(moduleName, out var evidence))
            {
                evidence = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                evidenceByModule[moduleName] = evidence;
            }

            if (evidence.Count < 12)
                evidence.Add(path);
        }

        foreach (var rawPath in filePaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                continue;

            var path = rawPath.Replace('\\', '/').Trim('/');
            var extension = Path.GetExtension(path);
            if (!CandidateExtensions.Contains(extension) || IsIgnoredPath(path))
                continue;

            foreach (var explicitModule in ExtractModuleDirectories(path))
                AddEvidence(HumanizeName(explicitModule), path);

            if (IsModuleBearingFile(path))
            {
                var candidate = HumanizeName(Path.GetFileNameWithoutExtension(path));
                if (IsUsefulDynamicModule(candidate))
                    AddEvidence(candidate, path);
            }
        }

        return evidenceByModule
            .Select(entry =>
            {
                var evidence = entry.Value.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
                var preview = string.Join(", ", evidence.Take(3));
                var suffix = evidence.Count > 3 ? $" and {evidence.Count - 3} more" : string.Empty;
                return new DetectedRepositoryModule(
                    entry.Key,
                    $"Detected from: {preview}{suffix}.",
                    evidence);
            })
            .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool PathMatchesAny(string path, IEnumerable<string> patterns) =>
        patterns.Any(pattern => path
            .Split('/', '\\')
            .Any(segment => segment.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

    private static bool IsIgnoredPath(string path) =>
        path.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/dist/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/vendor/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsModuleBearingFile(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;

        var fileName = Path.GetFileNameWithoutExtension(path);
        return fileName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Page", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Service", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Handler", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Request", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith("Response", StringComparison.OrdinalIgnoreCase) ||
               segments.Any(ModuleBearingDirectories.Contains);
    }

    private static IEnumerable<string> ExtractModuleDirectories(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!ModuleBearingDirectories.Contains(segments[index]))
                continue;

            if (index + 1 < segments.Length - 1 &&
                !ModuleBearingDirectories.Contains(segments[index + 1]))
                yield return segments[index + 1];
        }
    }

    private static string HumanizeName(string value)
    {
        var extension = Path.GetExtension(value);
        if (!string.IsNullOrEmpty(extension))
            value = Path.GetFileNameWithoutExtension(value);

        if (Regex.IsMatch(value, "^I[A-Z][a-z]"))
            value = value[1..];

        var name = Regex.Replace(value, "(?<=[A-Z])(?=[A-Z][a-z])", " ");
        name = Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
        name = Regex.Replace(name, "[-_.]+", " ");
        name = Regex.Replace(
            name,
            "\\b(View\\s+Model|Controller|Endpoint|Handler|Page|Service|Repository|Component|Model|Entity|Request|Response|Dto|Command|Query|Validator|Mapper|Worker|Job)$",
            string.Empty,
            RegexOptions.IgnoreCase);
        return Regex.Replace(name, "\\s+", " ").Trim();
    }

    private static string NormalizeModuleName(string value)
    {
        var name = HumanizeName(value);
        name = Regex.Replace(
            name,
            "^(Add|Create|Delete|Edit|Get|List|Manage|New|Remove|Update|View)\\s+",
            string.Empty,
            RegexOptions.IgnoreCase);
        name = Regex.Replace(
            name,
            "\\s+(Accepted|Approved|Completed|Details?|Form|List|Pending|Rejected)$",
            string.Empty,
            RegexOptions.IgnoreCase);

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return string.Empty;

        words[^1] = Singularize(words[^1]);
        return string.Join(' ', words);
    }

    private static string Singularize(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            return $"{word[..^3]}y";

        if (word.Length > 4 &&
            (word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("zes", StringComparison.OrdinalIgnoreCase)))
        {
            return word[..^2];
        }

        if (word.Length > 3 &&
            word.EndsWith('s') &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("is", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("us", StringComparison.OrdinalIgnoreCase))
        {
            return word[..^1];
        }

        return word;
    }

    private static bool IsUsefulDynamicModule(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length < 3 || GenericNames.Contains(candidate))
            return false;

        return !candidate.EndsWith("Context", StringComparison.OrdinalIgnoreCase) &&
               !candidate.EndsWith("Config", StringComparison.OrdinalIgnoreCase) &&
               !candidate.EndsWith("Settings", StringComparison.OrdinalIgnoreCase);
    }
}
