// DTch001A.cs - Memory cache service for API response caching

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;

namespace Alaris.Infrastructure.Data;

/// <summary>
/// Memory cache service for API response caching.
/// Component ID: DTch001A
/// </summary>
/// <remarks>
/// <para>
/// Provides caching for expensive API calls to reduce rate limit pressure and improve performance.
/// Implements sliding and absolute expiration policies.
/// </para>
/// <para>
/// Cache strategies:
/// <list type="bullet">
///   <item><description>Historical data: Long-lived (1 hour absolute)</description></item>
///   <item><description>Spot prices: Short-lived (5 minutes sliding)</description></item>
///   <item><description>Option chains: Medium-lived (15 minutes absolute)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class DTch001A
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DTch001A> _logger;

    /// <summary>
    /// Default cache duration for historical data (1 hour).
    /// </summary>
    public static readonly TimeSpan HistoricalDataExpiration = TimeSpan.FromHours(1);

    /// <summary>
    /// Default cache duration for spot prices (5 minutes sliding).
    /// </summary>
    public static readonly TimeSpan SpotPriceExpiration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default cache duration for option chains (15 minutes).
    /// </summary>
    public static readonly TimeSpan OptionChainExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of the cache service.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="logger">Logger instance.</param>
    public DTch001A(IMemoryCache cache, ILogger<DTch001A> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or sets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory function to create the value if not cached.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration time.</param>
    /// <param name="slidingExpiration">Optional sliding expiration time.</param>
    /// <returns>The cached or newly created value.</returns>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (_cache.TryGetValue<T>(key, out T? cachedValue))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return cachedValue!;
        }

        _logger.LogDebug("Cache miss for key: {Key}, fetching...", key);
        T value = await factory().ConfigureAwait(false);

        MemoryCacheEntryOptions options = new MemoryCacheEntryOptions();

        if (absoluteExpiration.HasValue)
        {
            options.SetAbsoluteExpiration(absoluteExpiration.Value);
        }

        if (slidingExpiration.HasValue)
        {
            options.SetSlidingExpiration(slidingExpiration.Value);
        }

        // Default to 30 minutes if no expiration specified
        if (!absoluteExpiration.HasValue && !slidingExpiration.HasValue)
        {
            options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
        }

        _cache.Set(key, value, options);
        _logger.LogDebug("Cached value for key: {Key}", key);

        return value;
    }

    /// <summary>
    /// Gets a cached value if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default if not found.</returns>
    public T? Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _cache.TryGetValue<T>(key, out T? value);
        return value;
    }

    /// <summary>
    /// Sets a cached value with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration time.</param>
    public void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        MemoryCacheEntryOptions options = new MemoryCacheEntryOptions();

        if (absoluteExpiration.HasValue)
        {
            options.SetAbsoluteExpiration(absoluteExpiration.Value);
        }
        else
        {
            options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
        }

        _cache.Set(key, value, options);
    }

    /// <summary>
    /// Removes a cached value.
    /// </summary>
    /// <param name="key">The cache key.</param>
    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _cache.Remove(key);
        _logger.LogDebug("Removed cache key: {Key}", key);
    }

    /// <summary>
    /// Generates a cache key for historical price data.
    /// </summary>
    public static string HistoricalBarsKey(string symbol, DateTime startDate, DateTime endDate) =>
        $"bars:{symbol}:{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";

    /// <summary>
    /// Generates a cache key for spot price data.
    /// </summary>
    public static string SpotPriceKey(string symbol) =>
        $"spot:{symbol}";

    /// <summary>
    /// Generates a cache key for option chain data.
    /// </summary>
    public static string OptionChainKey(string symbol, DateTime? asOfDate) =>
        asOfDate.HasValue ? $"chain:{symbol}:{asOfDate:yyyyMMdd}" : $"chain:{symbol}:live";

    /// <summary>
    /// Generates a cache key for treasury rate data.
    /// </summary>
    public static string TreasuryRateKey(DateTime date) =>
        $"treasury:{date:yyyyMMdd}";
}
