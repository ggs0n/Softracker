using WFHMonitor.Models;

namespace WFHMonitor.Services;

public static class BugSeverityResolver
{
    public static BugSeverity Resolve(string? explicitSeverity, params string?[] textSources)
    {
        var normalized = Normalize(explicitSeverity);
        if (TryParse(normalized, out var parsed))
            return parsed;

        var haystack = string.Join('\n', textSources.Where(t => !string.IsNullOrWhiteSpace(t)))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(haystack))
            return BugSeverity.Medium;

        if (ContainsAny(haystack,
            "critical",
            "sev0",
            "p0",
            "blocker",
            "outage",
            "service down",
            "data loss",
            "security breach",
            "rce",
            "sql injection",
            "privilege escalation",
            "authentication bypass"))
        {
            return BugSeverity.Critical;
        }

        if (ContainsAny(haystack,
            "high",
            "sev1",
            "p1",
            "crash",
            "payment failed",
            "cannot checkout",
            "cannot login",
            "broken flow",
            "production issue",
            "wrong total",
            "corrupt"))
        {
            return BugSeverity.High;
        }

        if (ContainsAny(haystack,
            "low",
            "sev3",
            "p3",
            "cosmetic",
            "alignment",
            "spacing",
            "typo",
            "copy issue",
            "minor ui"))
        {
            return BugSeverity.Low;
        }

        return BugSeverity.Medium;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private static bool TryParse(string value, out BugSeverity severity)
    {
        severity = BugSeverity.Medium;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value switch
        {
            "critical" or "sev0" or "p0" => Set(BugSeverity.Critical, out severity),
            "high" or "sev1" or "p1" => Set(BugSeverity.High, out severity),
            "medium" or "sev2" or "p2" => Set(BugSeverity.Medium, out severity),
            "low" or "sev3" or "p3" => Set(BugSeverity.Low, out severity),
            _ => false
        };
    }

    private static bool Set(BugSeverity value, out BugSeverity severity)
    {
        severity = value;
        return true;
    }
}
