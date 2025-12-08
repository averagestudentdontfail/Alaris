using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Alaris.Data.Model;

namespace Alaris.Data.Provider.Polygon;

/// <summary>
/// Polygon.io REST API client for market data retrieval.
/// Component ID: DTpl001A
/// </summary>
/// <remarks>
/// Implements DTpr003A (Market Data Provider interface) using Polygon.io Options Starter plan ($25/month).
/// - Provides 2 years historical options data
/// - Unlimited API calls
/// - 15-minute delayed quotes
/// - EOD updates at 5 PM ET
/// 
/// API Documentation: https://polygon.io/docs/options
/// </remarks>
public sealed class PolygonApiClient : DTpr003A
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PolygonApiClient> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.polygon.io";

    /// <summary>
    /// Initializes a new instance of the <see cref="PolygonApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="configuration">Configuration containing API key.</param>
    /// <param name="logger">Logger instance.</param>
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

        // Do not set BaseAddress on shared HttpClient
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

        // Subscription Limit Validation
        // Stocks Basic / Options Starter: 2 Years Historical Data
        // Use endDate as reference point for validation (not wall-clock time)
        // This allows backtesting to work correctly with simulation dates
        var referenceDate = endDate > DateTime.UtcNow.Date ? DateTime.UtcNow.Date : endDate;
        var minAllowedDate = referenceDate.AddYears(-2).Date;
        if (startDate < minAllowedDate)
        {
            var msg = $"Date range outside subscription limits. Start Date {startDate:yyyy-MM-dd} is older than 2 years from {referenceDate:yyyy-MM-dd} (Limit: {minAllowedDate:yyyy-MM-dd}). Upgrade to Stocks Starter for 5 years history.";
            _logger.LogError("Date range outside subscription limits. Start Date {StartDate} is older than limit {MinAllowedDate}.", startDate, minAllowedDate);
            throw new ArgumentOutOfRangeException(nameof(startDate), msg);
        }

        // Polygon aggregates endpoint: /v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{from}/{to}
        // Polygon aggregates endpoint: /v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{from}/{to}
        var url = $"{BaseUrl}/v2/aggs/ticker/{symbol}/range/1/day/{startDate:yyyy-MM-dd}/{endDate:yyyy-MM-dd}?adjusted=true&sort=asc&apiKey={_apiKey}";

        try
        {
            using var responseMessage = await _httpClient.GetAsync(new Uri(url, UriKind.Absolute), cancellationToken);
            
            if (!responseMessage.IsSuccessStatusCode)
            {
                var errorContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Polygon API request failed: {StatusCode} {ReasonPhrase}. Content: {Content}", 
                    responseMessage.StatusCode, responseMessage.ReasonPhrase, errorContent);
                throw new InvalidOperationException($"Polygon API failed: {responseMessage.StatusCode} - {errorContent}");
            }

            var response = await responseMessage.Content.ReadFromJsonAsync<PolygonAggregatesResponse>(cancellationToken: cancellationToken);

            if (response == null || response.Results == null || response.Results.Length == 0)
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
            _logger.LogError(ex, "Error fetching bars for {Symbol}", symbol);
            throw;
        }
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public Task<OptionChainSnapshot> GetOptionChainAsync(
        string symbol,
        DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        return GetHistoricalOptionChainAsync(symbol, asOfDate ?? DateTime.UtcNow.Date, cancellationToken);
    }

    /// <summary>
    /// Gets historical option chain for backtesting (as of a specific date).
    /// Uses Polygon's as_of parameter for contract listing and daily bars for pricing.
    /// </summary>
    /// <param name="symbol">Underlying symbol.</param>
    /// <param name="asOfDate">The historical date to fetch option chain for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Option chain snapshot as of the specified date.</returns>
    public async Task<OptionChainSnapshot> GetHistoricalOptionChainAsync(
        string symbol,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        // Clamp asOfDate to subscription limits (2 years for Options Starter)
        var referenceDate = asOfDate > DateTime.UtcNow.Date ? DateTime.UtcNow.Date : asOfDate;
        var twoYearsAgo = referenceDate.AddYears(-2).AddDays(1);
        var effectiveDate = asOfDate < twoYearsAgo ? twoYearsAgo : asOfDate;
        
        if (asOfDate < twoYearsAgo)
        {
            _logger.LogWarning("asOfDate {Date:yyyy-MM-dd} is outside 2-year subscription limit, using {EffectiveDate:yyyy-MM-dd}", 
                asOfDate, effectiveDate);
        }

        _logger.LogInformation("Fetching historical option chain for {Symbol} as of {Date:yyyy-MM-dd} (Grouped Daily)", symbol, effectiveDate);

        // 1. Get historical spot price (needed for IV calc)
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
            _logger.LogWarning("Spot price is 0, cannot calculate IV or filter effectively.");
        }

        // 2. Fetch Grouped Daily Bars for ALL Options on this date
        // API: /v2/aggs/grouped/locale/us/market/options/{date}
        var dateStr = effectiveDate.ToString("yyyy-MM-dd");
        var url = $"{BaseUrl}v2/aggs/grouped/locale/us/market/options/{dateStr}?adjusted=true&apiKey={_apiKey}";
        
        var contracts = new List<OptionContract>();
        
        try
        {
            // Note: This response can be large (100MB+ for whole market). 
            // Polygon doesn't support server-side filtering by underlying for Grouped Daily Options yet?
            // Actually, checking docs: Grouped Daily is for "Entire Market".
            // Optimization: If response is too huge, we might need a different approach.
            // But user claims "Unlimited API". Maybe we iterate contracts?
            // "Reference" gives us ~250 contracts. 250 calls to /v2/aggs/ticker/{ticker}/... is safer.
            // Let's stick to the Reference + Individual Query approach to avoid downloading 1GB of JSON daily.
            // The user has "Unlimited API Calls". Grouped Daily is bandwidth heavy.
            
            // Revert: Use Reference to get tickers, then fetch Bars parallel.
        
            // 2a. Fetch Reference Contracts (to get the list of symbols for this Underlying)
            var expirationMin = effectiveDate.ToString("yyyy-MM-dd");
            var expirationMax = effectiveDate.AddDays(60).ToString("yyyy-MM-dd");
            var refUrl = $"{BaseUrl}v3/reference/options/contracts?underlying_ticker={symbol}&as_of={dateStr}&expiration_date.gte={expirationMin}&expiration_date.lte={expirationMax}&limit=250&apiKey={_apiKey}";
            
            var refResponse = await _httpClient.GetFromJsonAsync<PolygonOptionsContractsResponse>(refUrl, cancellationToken);
            
            if (refResponse?.Results == null || refResponse.Results.Length == 0)
            {
                 _logger.LogWarning("No reference options found for {Symbol}", symbol);
                 return new OptionChainSnapshot { Symbol = symbol, SpotPrice = spotPrice, Timestamp = effectiveDate, Contracts = contracts };
            }
            
            _logger.LogInformation("Found {Count} reference contracts, fetching daily stats...", refResponse.Results.Length);

            // 2b. Fetch Daily Bar for each contract (Parallel)
            // Limit concurrency to avoid client saturation
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = cancellationToken };
            
            await Parallel.ForEachAsync(refResponse.Results, parallelOptions, async (refContract, token) =>
            {
                 try
                 {
                     // Fetch Daily Bar
                     // /v2/aggs/ticker/{ticker}/range/1/day/{date}/{date}
                     var barUrl = $"{BaseUrl}v2/aggs/ticker/{refContract.Ticker}/range/1/day/{dateStr}/{dateStr}?adjusted=true&apiKey={_apiKey}";
                     var barResponse = await _httpClient.GetFromJsonAsync<PolygonAggregatesResponse>(barUrl, token);
                     
                     if (barResponse?.Results != null && barResponse.Results.Length > 0)
                     {
                         var bar = barResponse.Results[0]; // Day bar
                         
                         var (underlying, strike, expiration, right) = ParseOptionTicker(refContract.Ticker);
                         
                         // Calculate IV (Newton Raphson)
                         decimal iv = 0m;
                         if (spotPrice > 0)
                         {
                             iv = CalculateImpliedVolatility(
                                 (double)bar.Close, 
                                 (double)spotPrice, 
                                 (double)strike, 
                                 (expiration - effectiveDate).TotalDays / 365.0, 
                                 0.05, // Risk free 5% approx necessary for IV
                                 right);
                         }

                         lock (contracts)
                         {
                             contracts.Add(new OptionContract
                             {
                                 UnderlyingSymbol = underlying,
                                 OptionSymbol = refContract.Ticker,
                                 Strike = strike,
                                 Expiration = expiration,
                                 Right = right,
                                 Bid = bar.Close, // Close as proxy for Bid/Ask
                                 Ask = bar.Close,
                                 Volume = (long)bar.Volume,
                                 OpenInterest = (long)bar.Volume, // Proxied from Volume (Aggrs only have Vol). Vol > 0 implies activity.
                                 ImpliedVolatility = iv,
                                 Timestamp = effectiveDate
                             });
                         }
                     }
                 }
                 catch (Exception) { /* Ignore individual failures */ }
            });
            
            _logger.LogInformation("Successfully hydrated {Count} contracts with price/volume/IV", contracts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch historical option chain details");
        }

        return new OptionChainSnapshot
        {
            Symbol = symbol,
            SpotPrice = spotPrice,
            Timestamp = asOfDate,
            Contracts = contracts
        };
    }

    // --- Helper for IV ---
    private static decimal CalculateImpliedVolatility(double price, double S, double K, double T, double r, OptionRight type)
    {
        if (T <= 0 || price <= 0) return 0m;
        
        // Simple Newton-Raphson
        double sigma = 0.5; // Initial guess
        for (int i = 0; i < 10; i++)
        {
             double volSquared = sigma * sigma;
             double numerator = Math.Log(S / K) + ((r + (volSquared / 2.0)) * T);
             double denominator = sigma * Math.Sqrt(T);
             
             double d1 = numerator / denominator;
             double d2 = d1 - denominator;
             
             double nd1 = NormalCdf(d1);
             double nd2 = NormalCdf(d2);
             
             double bsPrice;
             if (type == OptionRight.Call)
             {
                 double term1 = S * nd1;
                 double term2 = K * Math.Exp(-r * T) * nd2;
                 bsPrice = term1 - term2;
             }
             else
             {
                 double term1 = K * Math.Exp(-r * T) * (1 - nd2);
                 double term2 = S * (1 - nd1);
                 bsPrice = term1 - term2;
             }
                 
             double pdf = Math.Exp(-0.5 * d1 * d1) / Math.Sqrt(2 * Math.PI);
             double sqrtT = Math.Sqrt(T);
             double vega = S * sqrtT * pdf;
             
             if (Math.Abs(vega) < 1e-6) break;
             
             double diff = price - bsPrice;
             if (Math.Abs(diff) < 1e-4) return (decimal)sigma;
             
             sigma = sigma + (diff / vega);
             if (sigma <= 0) sigma = 0.01;
        }
        return (decimal)sigma;
    }
    
    private static double NormalCdf(double x)
    {
        // Approximation
        const double k = 0.044715;
        double sqrt2pi = Math.Sqrt(2 / Math.PI);
        double term = k * Math.Pow(x, 3);
        double argument = x * sqrt2pi * (1 + term);
        return 0.5 * (1 + Math.Tanh(argument)); 
    }


    /// <inheritdoc/>
    public async Task<decimal> GetSpotPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        // Get previous close: /v2/aggs/ticker/{ticker}/prev
        var url = $"{BaseUrl}/v2/aggs/ticker/{symbol}/prev?adjusted=true&apiKey={_apiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<PolygonAggregatesResponse>(
                url,
                cancellationToken: cancellationToken);

            if (response == null || response.Results == null || response.Results.Length == 0)
                throw new InvalidOperationException($"No spot price data for {symbol}");

            var spotPrice = response.Results[0].Close;
            _logger.LogDebug("Spot price for {Symbol}: {Price}", symbol, spotPrice);

            return spotPrice;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching spot price for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to fetch spot price for {symbol}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        DateTime? evaluationDate = null,
        CancellationToken cancellationToken = default)
    {
        // Calculate from last 30 days of bars
        // Use evaluationDate for backtests, or current date for live trading
        var endDate = (evaluationDate ?? DateTime.UtcNow).Date;
        var startDate = endDate.AddDays(-30);

        var bars = await GetHistoricalBarsAsync(symbol, startDate, endDate, cancellationToken);

        if (bars.Count == 0)
            throw new InvalidOperationException($"No historical data available for {symbol}");

        var avgVolume = (decimal)bars.Average(b => b.Volume);
        return avgVolume;
    }

    /// <summary>
    /// Gets a quote for a specific option ticker.
    /// </summary>
    private async Task<OptionContract> GetOptionQuoteAsync(
        string optionTicker,
        CancellationToken cancellationToken)
    {
        // Parse OCC format option ticker
        var (underlying, strike, expiration, right) = ParseOptionTicker(optionTicker);

        // Get snapshot quote: /v3/snapshot/options/{ticker}
        var url = $"{BaseUrl}/v3/snapshot/options/{underlying}/{optionTicker}?apiKey={_apiKey}";

        var response = await _httpClient.GetFromJsonAsync<PolygonOptionSnapshotResponse>(
            url,
            cancellationToken: cancellationToken);

        if (response?.Results?.Quote == null)
            throw new InvalidOperationException($"No quote data for {optionTicker}");

        var quote = response.Results.Quote;
        var greeks = response.Results.Greeks;

        return new OptionContract
        {
            UnderlyingSymbol = underlying,
            OptionSymbol = optionTicker,
            Strike = strike,
            Expiration = expiration,
            Right = right,
            Bid = quote.Bid ?? 0,
            Ask = quote.Ask ?? 0,
            Last = quote.Last,
            Volume = quote.Volume ?? 0,
            OpenInterest = response.Results.OpenInterest ?? 0,
            ImpliedVolatility = greeks?.Iv,
            Delta = greeks?.Delta,
            Gamma = greeks?.Gamma,
            Theta = greeks?.Theta,
            Vega = greeks?.Vega,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(quote.LastUpdated).DateTime
        };
    }

    /// <summary>
    /// Parses OCC format option ticker.
    /// Format: AAPL250117C00150000 = AAPL 2025-01-17 Call $150
    /// Polygon returns with O: prefix: O:AAPL250117C00150000
    /// </summary>
    private static (string underlying, decimal strike, DateTime expiration, OptionRight right) 
        ParseOptionTicker(string ticker)
    {
        // Strip Polygon's O: prefix if present
        if (ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
        {
            ticker = ticker[2..];
        }
        
        // OCC format: SYMBOL + YYMMDD + C/P + 00000000 (strike * 1000)
        // Example: AAPL250117C00150000
        
        var underlying = new string(ticker.TakeWhile(char.IsLetter).ToArray());
        var remaining = ticker[underlying.Length..];

        var dateStr = remaining[..6]; // YYMMDD
        var rightChar = remaining[6]; // C or P
        var strikeStr = remaining[7..]; // 00150000

        var expiration = DateTime.ParseExact($"20{dateStr}", "yyyyMMdd", null);
        var right = rightChar == 'C' ? OptionRight.Call : OptionRight.Put;
        var strike = decimal.Parse(strikeStr) / 1000m;

        return (underlying, strike, expiration, right);
    }
}

#region Polygon API Response Models

file sealed class PolygonAggregatesResponse
{
    [JsonPropertyName("results")]
    public PolygonBar[]? Results { get; init; }
}

file sealed class PolygonBar
{
    [JsonPropertyName("t")]
    public long Timestamp { get; init; }

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

file sealed class PolygonOptionsContractsResponse
{
    [JsonPropertyName("results")]
    public PolygonOptionContract[]? Results { get; init; }
}

file sealed class PolygonOptionContract
{
    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [JsonPropertyName("underlying_ticker")]
    public required string UnderlyingTicker { get; init; }
}

file sealed class PolygonOptionSnapshotResponse
{
    [JsonPropertyName("results")]
    public PolygonOptionDetails? Results { get; init; }
}

file sealed class PolygonOptionDetails
{
    [JsonPropertyName("last_quote")]
    public PolygonQuote? Quote { get; init; }

    [JsonPropertyName("greeks")]
    public PolygonGreeks? Greeks { get; init; }

    [JsonPropertyName("open_interest")]
    public long? OpenInterest { get; init; }
}

file sealed class PolygonQuote
{
    [JsonPropertyName("bid")]
    public decimal? Bid { get; init; }

    [JsonPropertyName("ask")]
    public decimal? Ask { get; init; }

    [JsonPropertyName("last")]
    public decimal? Last { get; init; }

    [JsonPropertyName("volume")]
    public long? Volume { get; init; }

    [JsonPropertyName("last_updated")]
    public long LastUpdated { get; init; }
}

file sealed class PolygonGreeks
{
    [JsonPropertyName("delta")]
    public decimal? Delta { get; init; }

    [JsonPropertyName("gamma")]
    public decimal? Gamma { get; init; }

    [JsonPropertyName("theta")]
    public decimal? Theta { get; init; }

    [JsonPropertyName("vega")]
    public decimal? Vega { get; init; }

    [JsonPropertyName("iv")]
    public decimal? Iv { get; init; }
}

#endregion