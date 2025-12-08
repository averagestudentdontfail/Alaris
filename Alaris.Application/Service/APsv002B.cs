// =============================================================================
// APsv002B.cs - Screener Service
// Component: APsv002B | Category: Services | Variant: B (Alternative)
// =============================================================================
// Automatic stock screener using Polygon API to generate symbol universe.
// Used by session creation when no explicit symbols are provided.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Alaris.Application.Service;

/// <summary>
/// Screener service that uses Polygon API to find tradeable stocks.
/// Component ID: APsv002B
/// </summary>
public sealed class APsv002B : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<APsv002B> _logger;
    private readonly string? _apiKey;
    
    // Default screening criteria (Atilgan 2014)
    private const decimal DefaultMinPrice = 5.00m;
    private const decimal DefaultMinDollarVolume = 1_500_000m;
    private const int DefaultMaxSymbols = 100;
    
    public APsv002B(HttpClient httpClient, IConfiguration configuration, ILogger<APsv002B> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Polygon:ApiKey"];
    }
    
    /// <summary>
    /// Screens stocks using Polygon grouped daily API and returns top symbols.
    /// </summary>
    /// <param name="date">Date to screen (uses this date's market data)</param>
    /// <param name="minPrice">Minimum stock price</param>
    /// <param name="minDollarVolume">Minimum dollar volume</param>
    /// <param name="maxSymbols">Maximum number of symbols to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of screened symbols sorted by dollar volume</returns>
    public async Task<List<string>> ScreenAsync(
        DateTime date,
        decimal minPrice = DefaultMinPrice,
        decimal minDollarVolume = DefaultMinDollarVolume,
        int maxSymbols = DefaultMaxSymbols,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Polygon API key not configured, using fallback symbols");
            return GetFallbackSymbols();
        }
        
        try
        {
            // Use the most recent trading day before the specified date
            var tradingDate = GetMostRecentTradingDay(date);
            var dateStr = tradingDate.ToString("yyyy-MM-dd");
            
            _logger.LogInformation("Screening stocks for {Date} with minPrice={MinPrice}, minVolume={MinVolume}", 
                dateStr, minPrice, minDollarVolume);
            
            var url = $"https://api.polygon.io/v2/aggs/grouped/locale/us/market/stocks/{dateStr}?adjusted=true&apiKey={_apiKey}";
            
            var response = await _httpClient.GetFromJsonAsync<PolygonGroupedResponse>(url, cancellationToken);
            
            if (response?.Results == null || response.Results.Length == 0)
            {
                _logger.LogWarning("No results from Polygon for {Date}, using fallback symbols", dateStr);
                return GetFallbackSymbols();
            }
            
            // Filter and sort by dollar volume
            var filteredSymbols = response.Results
                .Where(r => !string.IsNullOrEmpty(r.Ticker))
                .Where(r => !r.Ticker!.Contains('.'))  // Exclude preferred/class shares
                .Where(r => r.Ticker!.Length <= 5)     // Exclude warrants/units (usually > 5 chars)
                .Where(r => r.Close >= minPrice)
                .Where(r => r.Close * (decimal)r.Volume >= minDollarVolume)
                .OrderByDescending(r => r.Close * (decimal)r.Volume)
                .Take(maxSymbols)
                .Select(r => r.Ticker!.ToUpperInvariant())
                .ToList();
            
            _logger.LogInformation("Screened {Count} symbols from {Total} total", 
                filteredSymbols.Count, response.Results.Length);
            
            if (filteredSymbols.Count == 0)
            {
                _logger.LogWarning("No symbols passed screening, using fallback");
                return GetFallbackSymbols();
            }
            
            return filteredSymbols;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during screening, using fallback symbols");
            return GetFallbackSymbols();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during screening, using fallback symbols");
            return GetFallbackSymbols();
        }
    }
    
    /// <summary>
    /// Returns a minimal fallback list when screener unavailable.
    /// </summary>
    private static List<string> GetFallbackSymbols()
    {
        return new List<string> 
        { 
            "SPY", "QQQ", "IWM",           // ETFs for market exposure
            "AAPL", "MSFT", "GOOGL",       // Large-cap tech
            "AMZN", "NVDA", "TSLA", "META" // High-volume names
        };
    }
    
    /// <summary>
    /// Gets most recent trading day (skips weekends).
    /// </summary>
    private static DateTime GetMostRecentTradingDay(DateTime date)
    {
        var current = date.Date;
        
        // If date is in future, use yesterday
        if (current >= DateTime.Today)
        {
            current = DateTime.Today.AddDays(-1);
        }
        
        // Skip weekends
        while (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
        {
            current = current.AddDays(-1);
        }
        
        return current;
    }
    
    public void Dispose()
    {
        // HttpClient is externally managed
    }
}

#region Polygon API Response Models

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
    // Polygon stocks API uses uppercase "T" for ticker
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
}

#endregion

