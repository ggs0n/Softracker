namespace WFHMonitor.Configuration;

public sealed class ApiServiceProxySettings
{
    public const string SectionName = "ApiService";

    public string BaseUrl { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; }
    public string[] BackendPrefixes { get; set; } = [];
}

public sealed class AssetCachingSettings
{
    public const string SectionName = "AssetCaching";

    public int MaxAgeSeconds { get; set; }
}

public sealed class CompressionSettings
{
    public const string SectionName = "ResponseCompression";

    public string BrotliLevel { get; set; } = string.Empty;
    public string GzipLevel { get; set; } = string.Empty;
}
