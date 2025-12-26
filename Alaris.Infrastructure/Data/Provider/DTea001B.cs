// DTea001B.cs - SEC EDGAR earnings provider

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Provider.SEC;

/// <summary>
/// SEC EDGAR 8-K filings earnings provider.
/// Component ID: DTea001B
/// </summary>
/// <remarks>
/// <para>
/// Implements DTpr004A (Earnings Calendar Provider interface) using SEC EDGAR API.
/// </para>
/// <para>
/// Uses 8-K Form Item 2.02 ("Results of Operations and Financial Condition")
/// filings to detect earnings announcements. These filings are required within
/// 4 business days of an earnings release.
/// </para>
/// <para>
/// SEC API Requirements:
/// - User-Agent header with contact info (required)
/// - Rate limit: 10 requests/second
/// - No API key needed
/// </para>
/// </remarks>
public sealed class SecEdgarProvider : DTpr004A, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SecEdgarProvider> _logger;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly ConcurrentDictionary<string, string> _tickerToCikCache;
    private readonly ConcurrentDictionary<string, IReadOnlyList<SecFiling>> _filingCache;
    private bool _cikMappingLoaded;
    private readonly object _cikLoadLock = new();

    private const string SecBaseUrl = "https://data.sec.gov";
    private static readonly Uri SecTickerMappingUri = new("https://www.sec.gov/files/company_tickers.json");
    private const int RateLimitDelayMs = 110; // ~9 requests/second (conservative)
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="SecEdgarProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with SEC User-Agent.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when httpClient or logger is null.</exception>
    public SecEdgarProvider(
        HttpClient httpClient,
        ILogger<SecEdgarProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimiter = new SemaphoreSlim(1, 1);
        _tickerToCikCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _filingCache = new ConcurrentDictionary<string, IReadOnlyList<SecFiling>>();

        // Configure required SEC User-Agent header
        // SEC requires contact email in User-Agent; use TryAddWithoutValidation
        // since standard Add() rejects email format in header value
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Alaris/1.0 (contact@alaris.dev)");
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(
        string symbol,
        int daysAhead = 90,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation(
            "Fetching upcoming earnings for {Symbol} from SEC EDGAR ({Days} days ahead)",
            symbol, daysAhead);

        // SEC only has historical filings, not future dates
        // Return empty for "upcoming" - strictly avoiding inference heuristics
        _logger.LogWarning(
            "SEC EDGAR does not provide future earnings dates. Returning empty list for {Symbol}",
            symbol);

        return Task.FromResult<IReadOnlyList<EarningsEvent>>(Array.Empty<EarningsEvent>());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        // Check for session-based local cache (Backtesting Mode)
        var sessionDataPath = Environment.GetEnvironmentVariable("ALARIS_SESSION_DATA");
        if (!string.IsNullOrEmpty(sessionDataPath))
        {
            var cachePath = System.IO.Path.Combine(sessionDataPath, "earnings", $"{symbol.ToLowerInvariant()}.json");
            if (System.IO.File.Exists(cachePath))
            {
                _logger.LogInformation("Loading earnings for {Symbol} from local session cache: {Path}", symbol, cachePath);
                try
                {
                    using var stream = System.IO.File.OpenRead(cachePath);
                    var cachedEvents = await JsonSerializer.DeserializeAsync<List<EarningsEvent>>(stream, JsonOptions, cancellationToken);
                    if (cachedEvents != null)
                    {
                        return cachedEvents;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load cached earnings for {Symbol}", symbol);
                    // Fallback to API if cache load fails
                }
            }
        }

        _logger.LogInformation(
            "Fetching historical earnings for {Symbol} from SEC EDGAR ({Days} days back)",
            symbol, lookbackDays);

        var cik = await GetCikForTickerAsync(symbol, cancellationToken);
        if (string.IsNullOrEmpty(cik))
        {
            _logger.LogWarning("No CIK found for symbol {Symbol}", symbol);
            return Array.Empty<EarningsEvent>();
        }

        var filings = await GetCompanyFilingsAsync(cik, cancellationToken);

        // Return ALL Item 2.02 8-K filings - don't filter by DateTime.UtcNow
        // This is critical for backtesting where simulation date != current date
        // The caller (STUN001B) will filter relative to the simulation date
        var earningsFilings = filings
            .Where(f => f.Form == "8-K" && f.IsItem202Filing)
            .OrderByDescending(f => f.FilingDate)
            .ToList();

        _logger.LogInformation(
            "Found {Count} Item 2.02 8-K filings for {Symbol} (CIK: {Cik})",
            earningsFilings.Count, symbol, cik);

        return earningsFilings
            .Select(f => new EarningsEvent
            {
                Symbol = symbol,
                Date = f.FilingDate,
                FiscalQuarter = "Unknown", // Avoid heuristic inference
                FiscalYear = f.FilingDate.Year, // Using filing year as proxy, explicitly noted
                Timing = EarningsTiming.Unknown, // SEC doesn't specify BMO/AMC
                Source = "SEC-EDGAR",
                FetchedAt = DateTime.UtcNow
            })
            .ToList();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetSymbolsWithEarningsAsync not efficiently supported by SEC EDGAR. " +
            "Use ticker-specific queries instead.");

        // SEC EDGAR doesn't have a batch endpoint for earnings by date range
        // This would require querying all companies - not practical
        // Return empty and log warning
        _logger.LogWarning(
            "SEC EDGAR does not support date range queries. " +
            "Consider using FMP provider for this operation.");

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <inheritdoc/>
    public void EnableCacheOnlyMode()
    {
        // SEC EDGAR is already cache-aware via ALARIS_SESSION_DATA, no additional action needed
        _logger.LogDebug("EnableCacheOnlyMode called on SecEdgarProvider (cache already enabled)");
    }

    /// <summary>
    /// Gets the CIK number for a stock ticker.
    /// </summary>
    /// <param name="ticker">Stock ticker symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>10-digit padded CIK string, or null if not found.</returns>
    public async Task<string?> GetCikForTickerAsync(
        string ticker,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        var normalizedTicker = ticker.ToUpperInvariant().Trim();

        // Check cache first
        if (_tickerToCikCache.TryGetValue(normalizedTicker, out var cachedCik))
        {
            return cachedCik;
        }

        // Load CIK mapping if not already loaded
        await EnsureCikMappingLoadedAsync(cancellationToken);

        _tickerToCikCache.TryGetValue(normalizedTicker, out var cik);
        return cik;
    }

    /// <summary>
    /// Gets company filings from SEC EDGAR.
    /// </summary>
    private async Task<IReadOnlyList<SecFiling>> GetCompanyFilingsAsync(
        string cik,
        CancellationToken cancellationToken)
    {
        // Check cache
        if (_filingCache.TryGetValue(cik, out var cachedFilings))
        {
            return cachedFilings;
        }

        await RateLimitAsync(cancellationToken);

        var paddedCik = cik.PadLeft(10, '0');
        var url = $"{SecBaseUrl}/submissions/CIK{paddedCik}.json";

        try
        {
            _logger.LogDebug("Fetching SEC filings from: {Url}", url);

            var uri = new Uri(url);
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var submissions = JsonSerializer.Deserialize<SecSubmissions>(content, JsonOptions);

            if (submissions?.Filings?.Recent == null)
            {
                _logger.LogWarning("No filings found for CIK {Cik}", cik);
                return Array.Empty<SecFiling>();
            }

            var filings = ParseFilings(submissions.Filings.Recent);

            // Cache the results
            _filingCache[cik] = filings;

            return filings;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch SEC filings for CIK {Cik}", cik);
            throw new InvalidOperationException($"Failed to fetch SEC filings for CIK {cik}", ex);
        }
    }

    /// <summary>
    /// Parses SEC filing data into structured objects.
    /// </summary>
    private static IReadOnlyList<SecFiling> ParseFilings(SecRecentFilings recent)
    {
        if (recent.Form == null || recent.FilingDate == null || recent.PrimaryDocument == null)
        {
            return Array.Empty<SecFiling>();
        }

        int count = Math.Min(
            recent.Form.Count,
            Math.Min(recent.FilingDate.Count, recent.PrimaryDocument.Count));

        var filings = new List<SecFiling>(count);

        for (int i = 0; i < count; i++)
        {
            var form = recent.Form[i];
            var filingDateStr = recent.FilingDate[i];
            var primaryDoc = recent.PrimaryDocument[i];
            var items = i < recent.Items?.Count ? recent.Items[i] : null;

            if (!DateTime.TryParse(filingDateStr, out var filingDate))
            {
                continue;
            }

            filings.Add(new SecFiling
            {
                Form = form,
                FilingDate = filingDate,
                PrimaryDocument = primaryDoc,
                Items = items,
                // Item 2.02 = Results of Operations and Financial Condition
                IsItem202Filing = IsEarningsRelatedFiling(form, items)
            });
        }

        return filings;
    }

    /// <summary>
    /// Determines if a filing is earnings-related (Item 2.02).
    /// </summary>
    private static bool IsEarningsRelatedFiling(string form, string? items)
    {
        if (form != "8-K" && form != "8-K/A")
        {
            return false;
        }

        if (string.IsNullOrEmpty(items))
        {
            return false;
        }

        // Item 2.02 = Results of Operations and Financial Condition
        return items.Contains("2.02", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures CIK mapping is loaded from SEC.
    /// </summary>
    private async Task EnsureCikMappingLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cikMappingLoaded)
            return;

        lock (_cikLoadLock)
        {
            if (_cikMappingLoaded)
                return;
        }

        await RateLimitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Loading SEC ticker-to-CIK mapping");

            var response = await _httpClient.GetAsync(SecTickerMappingUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var mapping = JsonSerializer.Deserialize<Dictionary<string, SecCompanyTicker>>(content, JsonOptions);

            if (mapping != null)
            {
                foreach (var entry in mapping.Values)
                {
                    if (!string.IsNullOrEmpty(entry.Ticker) && entry.CikStr > 0)
                    {
                        var cik = entry.CikStr.ToString().PadLeft(10, '0');
                        _tickerToCikCache[entry.Ticker.ToUpperInvariant()] = cik;
                    }
                }

                _logger.LogInformation(
                    "Loaded {Count} ticker-to-CIK mappings",
                    _tickerToCikCache.Count);
            }

            lock (_cikLoadLock)
            {
                _cikMappingLoaded = true;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to load SEC ticker-to-CIK mapping");
            throw new InvalidOperationException("Failed to load SEC ticker-to-CIK mapping", ex);
        }
    }

    /// <summary>
    /// Enforces SEC rate limit.
    /// </summary>
    private async Task RateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            await Task.Delay(RateLimitDelayMs, cancellationToken);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }



    /// <inheritdoc/>
    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}


/// <summary>
/// SEC company submissions response.
/// </summary>
internal sealed class SecSubmissions
{
    [JsonPropertyName("cik")]
    public string? Cik { get; init; }

    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("tickers")]
    public List<string>? Tickers { get; init; }

    [JsonPropertyName("filings")]
    public SecFilings? Filings { get; init; }
}

/// <summary>
/// SEC filings container.
/// </summary>
internal sealed class SecFilings
{
    [JsonPropertyName("recent")]
    public SecRecentFilings? Recent { get; init; }
}

/// <summary>
/// SEC recent filings arrays.
/// </summary>
internal sealed class SecRecentFilings
{
    [JsonPropertyName("form")]
    public List<string>? Form { get; init; }

    [JsonPropertyName("filingDate")]
    public List<string>? FilingDate { get; init; }

    [JsonPropertyName("primaryDocument")]
    public List<string>? PrimaryDocument { get; init; }

    [JsonPropertyName("items")]
    public List<string>? Items { get; init; }
}

/// <summary>
/// Parsed SEC filing.
/// </summary>
internal sealed class SecFiling
{
    public required string Form { get; init; }
    public required DateTime FilingDate { get; init; }
    public required string PrimaryDocument { get; init; }
    public string? Items { get; init; }
    public bool IsItem202Filing { get; init; }
}

/// <summary>
/// SEC company ticker mapping entry.
/// </summary>
internal sealed class SecCompanyTicker
{
    [JsonPropertyName("cik_str")]
    public long CikStr { get; init; }

    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

