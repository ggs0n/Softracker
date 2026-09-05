namespace WFHMonitor.Configuration;

public sealed class DatabaseRuntimeSettings
{
    public const string SectionName = "Database";

    public int DbContextPoolSize { get; set; }
}

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    public string WebRootPath { get; set; } = string.Empty;
    public string UploadsDirectoryName { get; set; } = string.Empty;
    public int CacheMaxAgeSeconds { get; set; }
}

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class ApiCompressionSettings
{
    public const string SectionName = "ResponseCompression";

    public string BrotliLevel { get; set; } = string.Empty;
    public string GzipLevel { get; set; } = string.Empty;
}
