// DTpr001A.cs - Polygon.io REST API client for market data

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Provider.Polygon;

/// <summary>
/// Polygon.io REST API client for market data retrieval.
/// Component ID: DTpr001A
/// </summary>
public sealed class PolygonApiClient : DTpr003A
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PolygonApiClient> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.polygon.io"; // No trailing slash

    public PolygonApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PolygonApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _apiKey = configuration["Polygon:ApiKey"] 
            ?? throw new InvalidOperationException("Polygon API key not configured");
            
        var maskedKey = _apiKey.Length > 4 ? _apiKey[..4] + new string('*', _apiKey.Length - 4) : "INVALID";
        _logger.LogInformation("Polygon Provider initialized with Key: {MaskedKey}", maskedKey);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation(
            "Fetching historical bars for {Symbol} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            symbol, startDate, endDate);

        var referenceDate = endDate > DateTime.UtcNow.Date ? DateTime.UtcNow.Date : endDate;
        var minAllowedDate = referenceDate.AddYears(-2).Date;
        if (startDate < minAllowedDate)
        {
            _logger.LogError("Date range outside 2-year subscription limit. Start Date {StartDate:yyyy-MM-dd} is older than {MinAllowedDate:yyyy-MM-dd}", 
                startDate, minAllowedDate);
            throw new ArgumentOutOfRangeException(nameof(startDate), 
                $"Date range outside 2-year subscription limit. Start Date {startDate:yyyy-MM-dd} is older than {minAllowedDate:yyyy-MM-dd}.");
        }

        // FIX: Added "/" after BaseUrl
        var url = $"{BaseUrl}/v2/aggs/ticker/{symbol}/range/1/day/{startDate:yyyy-MM-dd}/{endDate:yyyy-MM-dd}?adjusted=true&sort=asc&apiKey={_apiKey}";

        try
        {
            using var responseMessage = await _httpClient.GetAsync(new Uri(url, UriKind.Absolute), cancellationToken);
            
            if (!responseMessage.IsSuccessStatusCode)
            {
                var errorContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Polygon API request failed: {StatusCode} {ReasonPhrase}. URL: {Url}. Content: {Content}", 
                    responseMessage.StatusCode, responseMessage.ReasonPhrase, url, errorContent);
                throw new InvalidOperationException($"Polygon API failed: {responseMessage.StatusCode} - {errorContent}");
            }

            var response = await responseMessage.Content.ReadFromJsonAsync<PolygonAggregatesResponse>(cancellationToken: cancellationToken);

            if (response?.Results == null || response.Results.Length == 0)
            {
                _logger.LogWarning("No data returned from Polygon for {Symbol}", symbol);
                return Array.Empty<PriceBar>();
            }

            var bars = response.Results.Select(r => new PriceBar
            {
                Symbol = symbol,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(r.Timestamp).DateTime,
                Open = r.Open,
                High = r.High,
                Low = r.Low,
                Close = r.Close,
                Volume = (long)r.Volume
            }).ToList();

            _logger.LogInformation("Retrieved {Count} bars for {Symbol}", bars.Count, symbol);
            return bars;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bars for {Symbol}. URL: {Url}", symbol, url);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<OptionChainSnapshot> GetOptionChainAsync(
        string symbol,
        DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        return GetHistoricalOptionChainAsync(symbol, asOfDate ?? DateTime.UtcNow.Date, cancellationToken);
    }

    /// <summary>
    /// Gets historical option chain for backtesting.
    /// NOTE: Polygon does NOT provide historical Greeks/IV via API.
    /// This method calculates IV from historical option prices using Black-Scholes,
    /// which is the industry-standard approach for backtesting.
    /// </summary>
    public async Task<OptionChainSnapshot> GetHistoricalOptionChainAsync(
        string symbol,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        var referenceDate = asOfDate > DateTime.UtcNow.Date ? DateTime.UtcNow.Date : asOfDate;
        var twoYearsAgo = referenceDate.AddYears(-2).AddDays(1);
        var effectiveDate = asOfDate < twoYearsAgo ? twoYearsAgo : asOfDate;
        
        if (asOfDate < twoYearsAgo)
        {
            _logger.LogWarning("asOfDate {Date:yyyy-MM-dd} is outside 2-year subscription limit, using {EffectiveDate:yyyy-MM-dd}", 
                asOfDate, effectiveDate);
        }

        _logger.LogInformation("Fetching historical option chain for {Symbol} as of {Date:yyyy-MM-dd}", symbol, effectiveDate);

        // 1. Get historical spot price
        var spotPrice = 0m;
        try
        {
            var spotStart = effectiveDate.AddDays(-5);
            if (spotStart < twoYearsAgo) spotStart = twoYearsAgo;
            var bars = await GetHistoricalBarsAsync(symbol, spotStart, effectiveDate, cancellationToken);
            spotPrice = bars.Count > 0 ? bars[bars.Count - 1].Close : 0m;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get historical spot price for {Symbol}", symbol);
        }

        if (spotPrice == 0)
        {
            _logger.LogWarning("Spot price is 0 for {Symbol}, option chain will be incomplete", symbol);
            return new OptionChainSnapshot { Symbol = symbol, SpotPrice = 0, Timestamp = effectiveDate, Contracts = new List<OptionContract>() };
        }

        // 2. Fetch option contracts using Reference API
        var dateStr = effectiveDate.ToString("yyyy-MM-dd");
        var expirationMin = effectiveDate.ToString("yyyy-MM-dd");
        var expirationMax = effectiveDate.AddDays(60).ToString("yyyy-MM-dd");
        
        // FIX: Added "/" after BaseUrl
        var refUrl = $"{BaseUrl}/v3/reference/options/contracts?underlying_ticker={symbol}&as_of={dateStr}&expiration_date.gte={expirationMin}&expiration_date.lte={expirationMax}&limit=250&apiKey={_apiKey}";
        
        _logger.LogDebug("Fetching reference contracts from: {Url}", refUrl);
        
        try
        {
            var refResponse = await _httpClient.GetFromJsonAsync<PolygonOptionsContractsResponse>(refUrl, cancellationToken);
            
            if (refResponse?.Results == null || refResponse.Results.Length == 0)
            {
                _logger.LogWarning("No reference options found for {Symbol} as of {Date}", symbol, dateStr);
                return new OptionChainSnapshot { Symbol = symbol, SpotPrice = spotPrice, Timestamp = effectiveDate, Contracts = new List<OptionContract>() };
            }
            
            _logger.LogInformation("Found {Count} reference contracts for {Symbol}, fetching daily bars (max 50)...", refResponse.Results.Length, symbol);

            // 3. Fetch daily bars for each contract - WITH PARALLELISM and LIMITS
            // Limit to 50 contracts to avoid excessive API calls (250 contracts * 50 symbols = 12,500 calls!)
            var contractsToFetch = refResponse.Results.Take(50).ToArray();
            var contracts = new System.Collections.Concurrent.ConcurrentBag<OptionContract>();
            var subscriptionLimitHit = false;
            
            // Use semaphore to limit concurrent requests (Polygon rate limits apply)
            using var semaphore = new SemaphoreSlim(5); // 5 concurrent requests
            var tasks = contractsToFetch.Select(async refContract =>
            {
                if (subscriptionLimitHit)
                    return;
                    
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var aggUrl = $"{BaseUrl}/v2/aggs/ticker/{refContract.Ticker}/range/1/day/{dateStr}/{dateStr}?adjusted=true&apiKey={_apiKey}";
                    
                    // Add timeout to prevent hanging
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                    
                    using var response = await _httpClient.GetAsync(new Uri(aggUrl, UriKind.Absolute), timeoutCts.Token);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                        if (errorBody.Contains("NOT_AUTHORIZED") || errorBody.Contains("plan doesn't include"))
                        {
                            _logger.LogWarning(
                                "Options data for {Date} is outside the 2-year historical data window. Skipping remaining contracts.",
                                dateStr);
                            subscriptionLimitHit = true;
                            return;
                        }
                        return; // Skip 403 errors silently
                    }
                    
                    if (!response.IsSuccessStatusCode)
                        return;
                    
                    var aggResponse = await response.Content.ReadFromJsonAsync<PolygonAggregatesResponse>(cancellationToken: timeoutCts.Token);
                    
                    if (aggResponse?.Results == null || aggResponse.Results.Length == 0)
                        return;

                    var bar = aggResponse.Results[0];
                    
                    // Parse contract details from OCC ticker format
                    var (underlying, strike, expiration, right) = ParseOptionTicker(refContract.Ticker);
                    
                    // Calculate IV using Black-Scholes (industry standard for backtesting)
                    var riskFreeRate = 0.05m;
                    var timeToExpiry = (decimal)(expiration - effectiveDate).TotalDays / 365.25m;
                    var impliedVol = CalculateImpliedVolatility(
                        spotPrice, strike, timeToExpiry, riskFreeRate, 
                        bar.Close, right == OptionRight.Call);
                    
                    contracts.Add(new OptionContract
                    {
                        OptionSymbol = refContract.Ticker,
                        UnderlyingSymbol = underlying,
                        Strike = strike,
                        Expiration = expiration,
                        Right = right,
                        Bid = Math.Max(0, bar.Close - 0.10m),
                        Ask = bar.Close + 0.10m,
                        Last = bar.Close,
                        Volume = (long)bar.Volume,
                        OpenInterest = (long)bar.Volume,
                        ImpliedVolatility = impliedVol > 0 ? impliedVol : null,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).DateTime
                    });
                }
                catch (OperationCanceledException)
                {
                    // Timeout - skip this contract
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch pricing for contract {Ticker}", refContract.Ticker);
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            await Task.WhenAll(tasks);

            _logger.LogInformation("Retrieved {Count} contracts with pricing for {Symbol}", contracts.Count, symbol);

            return new OptionChainSnapshot
            {
                Symbol = symbol,
                SpotPrice = spotPrice,
                Timestamp = effectiveDate,
                Contracts = contracts.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical option chain for {Symbol}. URL: {Url}", symbol, refUrl);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetSpotPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        // FIX: Added "/" after BaseUrl
        var url = $"{BaseUrl}/v2/aggs/ticker/{symbol}/prev?adjusted=true&apiKey={_apiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<PolygonAggregatesResponse>(url, cancellationToken);

            if (response?.Results == null || response.Results.Length == 0)
                throw new InvalidOperationException($"No spot price data for {symbol}");

            var spotPrice = response.Results[0].Close;
            _logger.LogDebug("Spot price for {Symbol}: {Price}", symbol, spotPrice);

            return spotPrice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spot price for {Symbol}. URL: {Url}", symbol, url);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        DateTime? evaluationDate = null,
        CancellationToken cancellationToken = default)
    {
        var endDate = (evaluationDate ?? DateTime.UtcNow).Date;
        var startDate = endDate.AddDays(-30);

        var bars = await GetHistoricalBarsAsync(symbol, startDate, endDate, cancellationToken);

        if (bars.Count == 0)
            throw new InvalidOperationException($"No historical data available for {symbol}");

        var avgVolume = (decimal)bars.Average(b => b.Volume);
        return avgVolume;
    }

    /// <summary>
    /// Calculates implied volatility using Newton-Raphson method on Black-Scholes model.
    /// This is necessary because Polygon does NOT provide historical IV via API.
    /// </summary>
    private decimal CalculateImpliedVolatility(
        decimal spotPrice,
        decimal strike,
        decimal timeToExpiry,
        decimal riskFreeRate,
        decimal marketPrice,
        bool isCall,
        int maxIterations = 100,
        decimal tolerance = 0.0001m)
    {
        if (marketPrice <= 0 || spotPrice <= 0 || timeToExpiry <= 0)
            return 0m;

        // Initial guess: ATM volatility
        var sigma = 0.30m;

        for (int i = 0; i < maxIterations; i++)
        {
            var theoreticalPrice = BlackScholesPrice(spotPrice, strike, timeToExpiry, riskFreeRate, sigma, isCall);
            var vega = BlackScholesVega(spotPrice, strike, timeToExpiry, riskFreeRate, sigma);

            if (vega < 0.000001m) break; // Avoid division by near-zero

            var priceDiff = theoreticalPrice - marketPrice;
            
            if (Math.Abs(priceDiff) < tolerance)
                return sigma;

            sigma -= priceDiff / vega; // Newton-Raphson step

            if (sigma <= 0 || sigma > 5m) // Invalid volatility range
                return 0m;
        }

        return sigma;
    }

    private decimal BlackScholesPrice(decimal S, decimal K, decimal T, decimal r, decimal sigma, bool isCall)
    {
        if (T <= 0) return Math.Max(isCall ? S - K : K - S, 0);

        var d1 = (Math.Log((double)(S / K)) + ((double)(r + (0.5m * sigma * sigma)) * (double)T)) / ((double)sigma * Math.Sqrt((double)T));
        var d2 = d1 - ((double)sigma * Math.Sqrt((double)T));

        if (isCall)
            return (decimal)(((double)S * NormalCDF(d1)) - ((double)K * Math.Exp(-(double)r * (double)T) * NormalCDF(d2)));
        else
            return (decimal)(((double)K * Math.Exp(-(double)r * (double)T) * NormalCDF(-d2)) - ((double)S * NormalCDF(-d1)));
    }

    private decimal BlackScholesVega(decimal S, decimal K, decimal T, decimal r, decimal sigma)
    {
        if (T <= 0) return 0;

        var d1 = (Math.Log((double)(S / K)) + ((double)(r + (0.5m * sigma * sigma)) * (double)T)) / ((double)sigma * Math.Sqrt((double)T));
        return (decimal)((double)S * Math.Sqrt((double)T) * NormalPDF(d1));
    }

    private double NormalPDF(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
    }

    private double NormalCDF(double x)
    {
        return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
    }

    private double Erf(double x)
    {
        // Abramowitz and Stegun approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        var t = 1.0 / (1.0 + (p * x));
        var y = 1.0 - (((((((((a5 * t) + a4) * t) + a3) * t) + a2) * t) + a1) * t * Math.Exp(-x * x));

        return sign * y;
    }

    private static (string underlying, decimal strike, DateTime expiration, OptionRight right) 
        ParseOptionTicker(string ticker)
    {
        // Remove "O:" prefix if present
        if (ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
            ticker = ticker[2..];
        
        // OCC format: UNDERLYING + YYMMDD + C/P + STRIKE(8 digits)
        // But some tickers have digits in the underlying (e.g., AMD1, BRK.A -> BRKA)
        // Strategy: Find C or P that's followed by exactly 8 digits (the strike)
        
        int cpIndex = -1;
        for (int i = ticker.Length - 9; i >= 6; i--) // Need at least 6 chars before (date)
        {
            var c = ticker[i];
            if ((c == 'C' || c == 'P') && 
                i + 9 == ticker.Length && // C/P + 8 strike digits = end of string
                ticker[(i + 1)..].All(char.IsDigit))
            {
                cpIndex = i;
                break;
            }
        }
        
        if (cpIndex < 6)
        {
            throw new FormatException($"Unable to parse option ticker: {ticker}");
        }
        
        var rightChar = ticker[cpIndex];
        var strikeStr = ticker[(cpIndex + 1)..];
        var dateStr = ticker[(cpIndex - 6)..cpIndex];
        var underlying = ticker[..(cpIndex - 6)];
        
        // Parse with 2-digit year prefix
        var expiration = DateTime.ParseExact($"20{dateStr}", "yyyyMMdd", null);
        var right = rightChar == 'C' ? OptionRight.Call : OptionRight.Put;
        var strike = decimal.Parse(strikeStr) / 1000m;

        return (underlying, strike, expiration, right);
    }
}


file sealed class PolygonAggregatesResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("results")]
    public PolygonBar[]? Results { get; init; }
}

file sealed class PolygonBar
{
    [System.Text.Json.Serialization.JsonPropertyName("t")]
    public long Timestamp { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("o")]
    public decimal Open { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("h")]
    public decimal High { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("l")]
    public decimal Low { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("c")]
    public decimal Close { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("v")]
    public double Volume { get; init; }
}

file sealed class PolygonOptionsContractsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("results")]
    public PolygonOptionContract[]? Results { get; init; }
}

file sealed class PolygonOptionContract
{
    [System.Text.Json.Serialization.JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("underlying_ticker")]
    public required string UnderlyingTicker { get; init; }
}

