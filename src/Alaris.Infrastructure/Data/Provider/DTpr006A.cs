// DTpr006A.cs - Polygon universe provider (grouped daily API)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Alaris.Infrastructure.Data.Provider;

/// <summary>
/// Polygon Universe Provider - fetches daily aggregates for all US stocks.
/// Component ID: DTpr006A
/// </summary>
/// <remarks>
/// Uses Polygon's Grouped Daily API to efficiently retrieve OHLCV data
/// for all US equities on a given date. This enables universe selection
/// without requiring QuantConnect's fundamental data subscription.
/// </remarks>
public sealed class PolygonUniverseProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PolygonUniverseProvider> _logger;
    private readonly string _apiKey;

    private const string BaseUrl = "https://api.polygon.io";

    // LoggerMessage delegates for high-performance structured logging
    private static readonly Action<ILogger, DateTime, int, Exception?> LogUniverseFetched =
        LoggerMessage.Define<DateTime, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogUniverseFetched)),
            "Fetched universe for {Date:yyyy-MM-dd}: {Count} stocks");

    private static readonly Action<ILogger, DateTime, int, decimal, decimal, Exception?> LogUniverseFiltered =
        LoggerMessage.Define<DateTime, int, decimal, decimal>(
            LogLevel.Information,
            new EventId(2, nameof(LogUniverseFiltered)),
            "Filtered universe for {Date:yyyy-MM-dd}: {Count} stocks (minVolume={MinVolume:C0}, minPrice={MinPrice:C2})");

    /// <summary>
    /// Initializes a new instance of the Polygon Universe Provider.
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls.</param>
    /// <param name="configuration">Configuration containing Polygon API key.</param>
    /// <param name="logger">Logger instance.</param>
    public PolygonUniverseProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PolygonUniverseProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _apiKey = configuration["Polygon:ApiKey"]
            ?? throw new InvalidOperationException("Polygon API key not configured");

        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    /// <summary>
    /// Gets all US stocks for a given date with their OHLCV data.
    /// </summary>
    /// <param name="date">The date to fetch data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of stock aggregates for the date.</returns>
    public async Task<IReadOnlyList<UniverseStock>> GetUniverseAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var url = $"/v2/aggs/grouped/locale/us/market/stocks/{dateStr}?adjusted=true&apiKey={_apiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<PolygonGroupedResponse>(url, cancellationToken)
                .ConfigureAwait(false);

            if (response?.Results == null || response.Results.Length == 0)
            {
                _logger.LogWarning("No data returned for {Date}", dateStr);
                return Array.Empty<UniverseStock>();
            }

            var stocks = response.Results
                .Where(r => !string.IsNullOrEmpty(r.Ticker))
                .Select(r => new UniverseStock
                {
                    Ticker = r.Ticker!,
                    Open = r.Open,
                    High = r.High,
                    Low = r.Low,
                    Close = r.Close,
                    Volume = r.Volume,
                    DollarVolume = r.Close * (decimal)r.Volume,
                    Date = date
                })
                .ToList();

            LogUniverseFetched(_logger, date, stocks.Count, null);
            return stocks;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch universe for {Date}", dateStr);
            throw;
        }
    }

    /// <summary>
    /// Gets filtered US stocks meeting volume and price criteria.
    /// </summary>
    /// <param name="date">The date to fetch data for.</param>
    /// <param name="minDollarVolume">Minimum dollar volume threshold.</param>
    /// <param name="minPrice">Minimum stock price threshold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of stocks meeting the filter criteria.</returns>
    public async Task<IReadOnlyList<UniverseStock>> GetFilteredUniverseAsync(
        DateTime date,
        decimal minDollarVolume = 1_500_000m,
        decimal minPrice = 5.00m,
        CancellationToken cancellationToken = default)
    {
        var allStocks = await GetUniverseAsync(date, cancellationToken).ConfigureAwait(false);

        var filtered = allStocks
            .Where(s => s.DollarVolume >= minDollarVolume)
            .Where(s => s.Close >= minPrice)
            .OrderByDescending(s => s.DollarVolume)
            .ToList();

        LogUniverseFiltered(_logger, date, filtered.Count, minDollarVolume, minPrice, null);
        return filtered;
    }

    /// <summary>
    /// Gets the list of tickers meeting universe criteria.
    /// </summary>
    /// <param name="date">The date to fetch data for.</param>
    /// <param name="minDollarVolume">Minimum dollar volume threshold.</param>
    /// <param name="minPrice">Minimum stock price threshold.</param>
    /// <param name="maxTickers">Maximum number of tickers to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of ticker symbols meeting criteria.</returns>
    public async Task<IReadOnlyList<string>> GetUniverseTickersAsync(
        DateTime date,
        decimal minDollarVolume = 1_500_000m,
        decimal minPrice = 5.00m,
        int maxTickers = 500,
        CancellationToken cancellationToken = default)
    {
        var filtered = await GetFilteredUniverseAsync(date, minDollarVolume, minPrice, cancellationToken)
            .ConfigureAwait(false);

        return filtered
            .Take(maxTickers)
            .Select(s => s.Ticker)
            .ToList();
    }
}

/// <summary>
/// Represents a stock in the universe with its daily aggregates.
/// </summary>
public sealed class UniverseStock
{
    /// <summary>Ticker symbol.</summary>
    public required string Ticker { get; init; }

    /// <summary>Opening price.</summary>
    public decimal Open { get; init; }

    /// <summary>High price.</summary>
    public decimal High { get; init; }

    /// <summary>Low price.</summary>
    public decimal Low { get; init; }

    /// <summary>Closing price.</summary>
    public decimal Close { get; init; }

    /// <summary>Trading volume.</summary>
    public double Volume { get; init; }

    /// <summary>Dollar volume (Close * Volume).</summary>
    public decimal DollarVolume { get; init; }

    /// <summary>Date of the data.</summary>
    public DateTime Date { get; init; }
}


file sealed class PolygonGroupedResponse
{
    [JsonPropertyName("results")]
    public PolygonGroupedResult[]? Results { get; init; }

    [JsonPropertyName("resultsCount")]
    public int ResultsCount { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

file sealed class PolygonGroupedResult
{
    [JsonPropertyName("T")]
    public string? Ticker { get; init; }

    [JsonPropertyName("o")]
    public decimal Open { get; init; }

    [JsonPropertyName("h")]
    public decimal High { get; init; }

    [JsonPropertyName("l")]
    public decimal Low { get; init; }

    [JsonPropertyName("c")]
    public decimal Close { get; init; }

    [JsonPropertyName("v")]
    public double Volume { get; init; }

    [JsonPropertyName("vw")]
    public decimal VolumeWeightedAvgPrice { get; init; }

    [JsonPropertyName("n")]
    public int NumberOfTransactions { get; init; }
}

