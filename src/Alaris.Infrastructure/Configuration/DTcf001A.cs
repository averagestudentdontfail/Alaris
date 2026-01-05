// DTcf001A.cs - Typed configuration options for API providers

using System.ComponentModel.DataAnnotations;

namespace Alaris.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Polygon.io API.
/// Component ID: DTcf001A (Polygon section)
/// </summary>
public sealed class PolygonOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Polygon";

    /// <summary>
    /// API key for Polygon.io.
    /// </summary>
    [Required]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Base URL for the Polygon API (default: https://api.polygon.io).
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.polygon.io";

    /// <summary>
    /// Request timeout in seconds (default: 30).
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Maximum concurrent requests (default: 5 for free tier, higher for paid).
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrentRequests { get; init; } = 5;
}

/// <summary>
/// Configuration options for Treasury Direct API.
/// Component ID: DTcf001A (Treasury section)
/// </summary>
public sealed class TreasuryOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Treasury";

    /// <summary>
    /// Base URL for Treasury Direct API.
    /// </summary>
    public string BaseUrl { get; init; } = "https://www.treasurydirect.gov/TA_WS/securities/";

    /// <summary>
    /// Request timeout in seconds (default: 30).
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Number of days to look back for current rate (default: 7).
    /// </summary>
    [Range(1, 30)]
    public int LookbackDays { get; init; } = 7;
}

/// <summary>
/// Configuration options for SEC EDGAR API.
/// Component ID: DTcf001A (SecEdgar section)
/// </summary>
public sealed class SecEdgarOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SecEdgar";

    /// <summary>
    /// User-Agent email for SEC EDGAR (required by SEC).
    /// </summary>
    [Required]
    [EmailAddress]
    public required string ContactEmail { get; init; }

    /// <summary>
    /// Base URL for SEC EDGAR API.
    /// </summary>
    public string BaseUrl { get; init; } = "https://data.sec.gov";

    /// <summary>
    /// Request timeout in seconds (default: 30).
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Rate limit delay in milliseconds between requests (SEC requires 10 req/sec max).
    /// </summary>
    [Range(100, 5000)]
    public int RateLimitDelayMs { get; init; } = 100;
}
