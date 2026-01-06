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
    /// Maximum concurrent requests (default: 25, tune to your plan).
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrentRequests { get; init; } = 25;

    /// <summary>
    /// Target request rate per second for Polygon API calls.
    /// </summary>
    [Range(1, 500)]
    public int RequestsPerSecond { get; init; } = 100;

    /// <summary>
    /// Maximum number of option contracts to fetch per chain.
    /// </summary>
    [Range(1, 500)]
    public int OptionsContractLimit { get; init; } = 20;

    /// <summary>
    /// Number of near-ATM contracts to fetch per expiry per right (call/put).
    /// </summary>
    [Range(1, 50)]
    public int OptionsContractsPerExpiryRight { get; init; } = 2;

    /// <summary>
    /// Maximum number of expirations to consider when sampling a chain.
    /// </summary>
    [Range(1, 24)]
    public int OptionsMaxExpirations { get; init; } = 3;

    /// <summary>
    /// Maximum number of option chains to fetch in parallel.
    /// </summary>
    [Range(1, 50)]
    public int OptionsChainParallelism { get; init; } = 4;

    /// <summary>
    /// Delay in milliseconds between option chain requests.
    /// </summary>
    [Range(0, 10000)]
    public int OptionsChainDelayMs { get; init; } = 0;

    /// <summary>
    /// Minimum spacing in days between option chain snapshots during bootstrap.
    /// </summary>
    [Range(1, 30)]
    public int OptionsBootstrapStrideDays { get; init; } = 1;

    /// <summary>
    /// Cache window (days) for reusing option reference contract lists.
    /// </summary>
    [Range(0, 90)]
    public int OptionsContractListCacheDays { get; init; } = 7;

    /// <summary>
    /// Right filter for option contracts: "both", "call", or "put".
    /// </summary>
    public string OptionsRightFilter { get; init; } = "both";

    /// <summary>
    /// Scheduler weight for daily bars endpoint.
    /// </summary>
    [Range(1, 20)]
    public int EndpointWeightDailyBars { get; init; } = 1;

    /// <summary>
    /// Scheduler weight for options contracts reference endpoint.
    /// </summary>
    [Range(1, 20)]
    public int EndpointWeightOptionsContracts { get; init; } = 1;

    /// <summary>
    /// Scheduler weight for option aggregates endpoint.
    /// </summary>
    [Range(1, 50)]
    public int EndpointWeightOptionAggregates { get; init; } = 6;

    /// <summary>
    /// Scheduler weight for previous-day endpoint.
    /// </summary>
    [Range(1, 20)]
    public int EndpointWeightPreviousDay { get; init; } = 1;
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
