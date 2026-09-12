namespace WFHMonitor.Services;

public static class QaTestNumberParser
{
    public static int ParseSequence(string? testNumber)
    {
        if (string.IsNullOrWhiteSpace(testNumber))
            return 0;

        var parts = testNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 && int.TryParse(parts[2], out var sequence)
            ? sequence
            : 0;
    }
}
