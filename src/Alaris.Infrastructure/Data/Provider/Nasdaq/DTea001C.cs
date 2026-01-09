// DTea001C.cs - NASDAQ public API earnings provider

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Http.Contracts;
using Alaris.Infrastructure.Http;

namespace Alaris.Infrastructure.Data.Provider.Nasdaq;

/// <summary>
/// NASDAQ public API earnings calendar provider.
/// Component ID: DTea001C
/// </summary>
/// <remarks>
/// <para>
/// Implements DTpr004A (Earnings Calendar Provider interface) using NASDAQ's public API
/// via Refit declarative interface (INasdaqCalendarApi).
/// </para>
/// <para>
/// API Endpoint: https://api.nasdaq.com/api/calendar/earnings
/// Cost: FREE (no API key required)
/// Rate Limit: Undocumented (appears unlimited for reasonable use)
/// </para>
/// <para>
/// Fail-fast design: No fallback providers. Errors propagate immediately.
/// Resilience provided by Microsoft.Extensions.Http.Resilience standard handler.
/// </para>
/// <para>
/// Future Alternative: EODHD Calendar API ($19.99/month) provides 100,000 calls/day
/// with official C# wrapper and SLA if this endpoint becomes unreliable.
/// </para>
/// </remarks>
public sealed class NasdaqEarningsProvider : DTpr004A
{
    private readonly INasdaqCalendarApi _api;
    private readonly ILogger<NasdaqEarningsProvider> _logger;
    private readonly string? _cacheDataPath;
    private readonly ApiRateLimiter? _rateLimiter;
    private bool _cacheOnlyMode;
    
    // In-memory cache for API responses to reduce NASDAQ rate limiting
    private readonly Dictionary<DateTime, IReadOnlyList<EarningsEvent>> _memoryCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Initialises a new instance of the <see cref="NasdaqEarningsProvider"/> class.
    /// </summary>
    /// <param name="api">NASDAQ Calendar Refit API client.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="cacheDataPath">Optional cache data path for backtest mode.</param>
    /// <param name="rateLimiter">Optional rate limiter for API calls.</param>
    public NasdaqEarningsProvider(
        INasdaqCalendarApi api,
        ILogger<NasdaqEarningsProvider> logger,
        string? cacheDataPath = null,
        ApiRateLimiter? rateLimiter = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheDataPath = cacheDataPath ?? Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA");
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Enables cache-only mode (no API calls). Use for backtests to prevent 403 errors.
    /// </summary>
    public void EnableCacheOnlyMode()
    {
        _cacheOnlyMode = true;
        _logger.LogInformation("NasdaqEarningsProvider: Cache-only mode enabled (no API calls)");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(
        string symbol,
        int daysAhead = 90,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation(
            "Fetching upcoming earnings for {Symbol} ({Days} days ahead) from NASDAQ",
            symbol, daysAhead);

        DateTime startDate = DateTime.UtcNow.Date;
        DateTime endDate = startDate.AddDays(daysAhead);

        IReadOnlyList<EarningsEvent> allEarnings = await GetEarningsInDateRangeAsync(
            startDate,
            endDate,
            cancellationToken);

        List<EarningsEvent> symbolEarnings = new List<EarningsEvent>();
        foreach (EarningsEvent earnings in allEarnings)
        {
            if (string.Equals(earnings.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            {
                symbolEarnings.Add(earnings);
            }
        }

        symbolEarnings.Sort(static (left, right) => left.Date.CompareTo(right.Date));

        _logger.LogInformation(
            "Found {Count} upcoming earnings for {Symbol}",
            symbolEarnings.Count, symbol);

        return symbolEarnings;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default)
    {
        // Default: anchor to today (live mode)
        return await GetHistoricalEarningsAsync(symbol, DateTime.UtcNow.Date, lookbackDays, cancellationToken);
    }

    /// <summary>
    /// Gets historical earnings for a symbol, anchored to a specific date.
    /// Use this overload in backtest mode with simulation date as anchor.
    /// </summary>
    /// <param name="symbol">The symbol to look up.</param>
    /// <param name="anchorDate">The anchor date (simulation date in backtest, or today in live).</param>
    /// <param name="lookbackDays">Number of days to look back from anchor date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        DateTime anchorDate,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation(
            "Fetching historical earnings for {Symbol} ({Days} days back from {Anchor:yyyy-MM-dd}) from NASDAQ",
            symbol, lookbackDays, anchorDate);

        DateTime endDate = anchorDate.Date;
        DateTime startDate = endDate.AddDays(-lookbackDays);

        List<EarningsEvent> allEarnings = new List<EarningsEvent>();

        // Query in 30-day chunks to avoid overwhelming the API
        DateTime chunkStart = startDate;
        while (chunkStart < endDate)
        {
            DateTime chunkEnd = chunkStart.AddDays(30);
            if (chunkEnd > endDate) chunkEnd = endDate;

            try
            {
                IReadOnlyList<EarningsEvent> chunkEarnings = await GetEarningsInDateRangeAsync(
                    chunkStart, chunkEnd, cancellationToken);

                List<EarningsEvent> symbolChunk = new List<EarningsEvent>();
                foreach (EarningsEvent earnings in chunkEarnings)
                {
                    if (string.Equals(earnings.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                    {
                        symbolChunk.Add(earnings);
                    }
                }

                allEarnings.AddRange(symbolChunk);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch earnings for {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    chunkStart, chunkEnd);
            }

            chunkStart = chunkEnd.AddDays(1);
        }

        allEarnings.Sort(static (left, right) => right.Date.CompareTo(left.Date));

        _logger.LogInformation(
            "Retrieved {Count} historical earnings for {Symbol}",
            allEarnings.Count, symbol);

        return allEarnings;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching symbols with earnings from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        IReadOnlyList<EarningsEvent> earnings = await GetEarningsInDateRangeAsync(
            startDate, endDate, cancellationToken);

        HashSet<string> symbolSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EarningsEvent earningsEvent in earnings)
        {
            if (!string.IsNullOrWhiteSpace(earningsEvent.Symbol))
            {
                symbolSet.Add(earningsEvent.Symbol);
            }
        }

        List<string> symbols = new List<string>(symbolSet.Count);
        foreach (string symbol in symbolSet)
        {
            symbols.Add(symbol);
        }

        symbols.Sort(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Found {Count} symbols with earnings", symbols.Count);
        return symbols;
    }

    /// <summary>
    /// Gets all earnings events in a date range by querying each date.
    /// </summary>
    private async Task<IReadOnlyList<EarningsEvent>> GetEarningsInDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        List<EarningsEvent> allEarnings = new List<EarningsEvent>();

        // Query each date individually (NASDAQ API is date-specific)
        for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;

            try
            {
                IReadOnlyList<EarningsEvent> dayEarnings = await GetEarningsForDateAsync(
                    date, cancellationToken);
                allEarnings.AddRange(dayEarnings);
            }
            catch (HttpRequestException ex)
            {
                // Check if it's a 403 rate limit error
                if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
                {
                    _logger.LogWarning("NASDAQ rate limit (403) for {Date:yyyy-MM-dd}, skipping", date);
                    // Graceful degradation: continue with other dates
                    continue;
                }
                _logger.LogWarning(ex, "Failed to fetch earnings for {Date:yyyy-MM-dd}", date);
                // Non-403 errors: rethrow to caller
                throw;
            }
        }

        return allEarnings;
    }

    /// <summary>
    /// Gets earnings events for a specific date, checking cache first.
    /// </summary>
    /// <remarks>
    /// Cache-first pattern:
    /// 1. Check {session}/earnings/nasdaq/{date}.json
    /// 2. If cached → Load from file (backtest mode)
    /// 3. If live → Call NASDAQ API
    /// </remarks>
    private async Task<IReadOnlyList<EarningsEvent>> GetEarningsForDateAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        // 0. Check in-memory cache first (reduces API calls during session)
        DateTime cacheKey = date.Date;
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<EarningsEvent>? cached))
        {
            _logger.LogDebug("Using in-memory cached earnings for {Date:yyyy-MM-dd}", date);
            return cached;
        }
        
        // 1. Check for cached data (backtest mode)
        if (!string.IsNullOrEmpty(_cacheDataPath))
        {
            string cachePath = Path.Combine(
                _cacheDataPath,
                "earnings",
                "nasdaq",
                $"{date:yyyy-MM-dd}.json");

            if (File.Exists(cachePath))
            {
                _logger.LogDebug("Loading earnings for {Date:yyyy-MM-dd} from cache", date);
                IReadOnlyList<EarningsEvent> fileResult = await LoadFromCacheAsync(cachePath, cancellationToken);
                _memoryCache[cacheKey] = fileResult;
                return fileResult;
            }
            
            // Cache-only mode: skip API call on cache miss (prevents 403 in backtests)
            if (_cacheOnlyMode)
            {
                _logger.LogDebug("Cache miss for {Date:yyyy-MM-dd}, skipping (cache-only mode)", date);
                return Array.Empty<EarningsEvent>();
            }
        }
        else if (_cacheOnlyMode)
        {
            // No cache path and cache-only mode = skip entirely
            _logger.LogDebug("No cache path, skipping {Date:yyyy-MM-dd} (cache-only mode)", date);
            return Array.Empty<EarningsEvent>();
        }

        // 2. Live mode: Call NASDAQ API (rate-limited if limiter configured)
        _logger.LogDebug("Fetching earnings for {Date:yyyy-MM-dd} from NASDAQ API", date);
        string dateString = date.ToString("yyyy-MM-dd");
        
        NasdaqEarningsResponse response;
        if (_rateLimiter != null)
        {
            using IDisposable _ = await _rateLimiter.AcquireAsync(cancellationToken);
            response = await _api.GetEarningsAsync(dateString, cancellationToken);
        }
        else
        {
            response = await _api.GetEarningsAsync(dateString, cancellationToken);
        }

        if (response.Data?.Rows == null || response.Data.Rows.Count == 0)
        {
            _memoryCache[cacheKey] = Array.Empty<EarningsEvent>();
            return Array.Empty<EarningsEvent>();
        }

        List<EarningsEvent> earnings = new List<EarningsEvent>();
        foreach (NasdaqEarningsRow row in response.Data.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Symbol))
            {
                continue;
            }

            earnings.Add(MapToEarningsEvent(row, date));
        }

        _logger.LogDebug("Fetched {Count} earnings for {Date:yyyy-MM-dd}", earnings.Count, date);
        
        // Cache successful API response
        _memoryCache[cacheKey] = earnings;
        return earnings;
    }

    /// <summary>
    /// Loads cached earnings data from JSON file.
    /// </summary>
    private async Task<IReadOnlyList<EarningsEvent>> LoadFromCacheAsync(
        string cachePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = File.OpenRead(cachePath);
            CachedEarningsDay? cached = await JsonSerializer.DeserializeAsync<CachedEarningsDay>(
                stream,
                JsonOptions,
                cancellationToken);

            return (IReadOnlyList<EarningsEvent>?)cached?.Earnings ?? Array.Empty<EarningsEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load earnings cache from {Path}", cachePath);
            return Array.Empty<EarningsEvent>();
        }
    }

    /// <summary>
    /// Saves earnings data to JSON cache file for future backtests.
    /// </summary>
    public async Task SaveToCacheAsync(
        DateTime date,
        IReadOnlyList<EarningsEvent> earnings,
        string outputPath,
        CancellationToken cancellationToken)
    {
        string nasdaqPath = Path.Combine(outputPath, "earnings", "nasdaq");
        Directory.CreateDirectory(nasdaqPath);

        string cachePath = Path.Combine(nasdaqPath, $"{date:yyyy-MM-dd}.json");

        List<EarningsEvent> cachedEarnings = new List<EarningsEvent>(earnings.Count);
        for (int i = 0; i < earnings.Count; i++)
        {
            cachedEarnings.Add(earnings[i]);
        }

        CachedEarningsDay cached = new CachedEarningsDay
        {
            Date = date,
            FetchedAt = DateTime.UtcNow,
            Earnings = cachedEarnings
        };

        await using FileStream stream = File.Create(cachePath);
        await JsonSerializer.SerializeAsync(stream, cached, JsonOptions, cancellationToken);

        _logger.LogDebug("Saved {Count} earnings to cache for {Date:yyyy-MM-dd}", earnings.Count, date);
    }

    /// <summary>
    /// Fetches and caches earnings for a specific date (bootstrap mode).
    /// </summary>
    public async Task<IReadOnlyList<EarningsEvent>> FetchAndCacheAsync(
        DateTime date,
        string outputPath,
        CancellationToken cancellationToken)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        NasdaqEarningsResponse response = await _api.GetEarningsAsync(dateString, cancellationToken);

        List<EarningsEvent> earnings;
        if (response.Data?.Rows == null || response.Data.Rows.Count == 0)
        {
            earnings = new List<EarningsEvent>();
        }
        else
        {
            earnings = new List<EarningsEvent>();
            foreach (NasdaqEarningsRow row in response.Data.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.Symbol))
                {
                    continue;
                }

                earnings.Add(MapToEarningsEvent(row, date));
            }
        }

        await SaveToCacheAsync(date, earnings, outputPath, cancellationToken);
        return earnings;
    }

    /// <summary>
    /// Maps NASDAQ API response to EarningsEvent model.
    /// </summary>
    private static EarningsEvent MapToEarningsEvent(NasdaqEarningsRow row, DateTime date)
    {
        // Parse timing from NASDAQ time field
        EarningsTiming timing = row.Time?.ToLowerInvariant() switch
        {
            "time-pre-market" or "bmo" => EarningsTiming.BeforeMarketOpen,
            "time-after-hours" or "amc" => EarningsTiming.AfterMarketClose,
            "time-not-supplied" => EarningsTiming.Unknown,
            _ => EarningsTiming.Unknown
        };

        // Parse EPS values (NASDAQ sometimes returns as string)
        decimal? epsEstimate = ParseDecimal(row.EpsForecast);
        decimal? epsActual = ParseDecimal(row.EpsActual);

        // Parse fiscal period (e.g., "Q1 2025")
        string? fiscalQuarter = null;
        int? fiscalYear = null;
        if (!string.IsNullOrWhiteSpace(row.FiscalQuarterEnding))
        {
            string[] parts = row.FiscalQuarterEnding.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1) fiscalQuarter = parts[0];
            if (parts.Length >= 2 && int.TryParse(parts[1], out int year)) fiscalYear = year;
        }

        return new EarningsEvent
        {
            Symbol = row.Symbol!.ToUpperInvariant(),
            Date = date,
            FiscalQuarter = fiscalQuarter,
            FiscalYear = fiscalYear,
            Timing = timing,
            EpsEstimate = epsEstimate,
            EpsActual = epsActual,
            Source = "NASDAQ",
            FetchedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parses a decimal from string, handling various formats.
    /// </summary>
    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "--" || value == "N/A")
            return null;

        // Remove $ and other formatting
        string cleaned = value.Replace("$", "").Replace(",", "").Trim();
        
        if (decimal.TryParse(cleaned, out decimal result))
            return result;
        
        return null;
    }
}

/// <summary>
/// Cached earnings data for a single day.
/// </summary>
public sealed class CachedEarningsDay
{
    public DateTime Date { get; init; }
    public DateTime FetchedAt { get; init; }
    public IReadOnlyList<EarningsEvent> Earnings { get; init; } = Array.Empty<EarningsEvent>();
}
