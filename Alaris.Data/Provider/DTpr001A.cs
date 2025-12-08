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

        _httpClient.BaseAddress = new Uri(BaseUrl);
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
        var minAllowedDate = DateTime.UtcNow.AddYears(-2).Date;
        if (startDate < minAllowedDate)
        {
            var msg = $"Date range outside subscription limits. Start Date {startDate:yyyy-MM-dd} is older than 2 years (Limit: {minAllowedDate:yyyy-MM-dd}). Upgrade to Stocks Starter for 5 years history.";
            _logger.LogError("Date range outside subscription limits. Start Date {StartDate} is older than limit {MinAllowedDate}.", startDate, minAllowedDate);
            throw new ArgumentOutOfRangeException(nameof(startDate), msg);
        }

        // Polygon aggregates endpoint: /v2/aggs/ticker/{ticker}/range/{multiplier}/{timespan}/{from}/{to}
        var url = $"/v2/aggs/ticker/{symbol}/range/1/day/{startDate:yyyy-MM-dd}/{endDate:yyyy-MM-dd}?adjusted=true&sort=asc&apiKey={_apiKey}";

        try
        {
            using var responseMessage = await _httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken);
            
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
    public async Task<OptionChainSnapshot> GetOptionChainAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation("Fetching option chain for {Symbol}", symbol);

        // First get current spot price
        var spotPrice = await GetSpotPriceAsync(symbol, cancellationToken);

        // Get options contracts: /v3/reference/options/contracts
        var url = $"/v3/reference/options/contracts?underlying_ticker={symbol}&limit=1000&apiKey={_apiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<PolygonOptionsContractsResponse>(
                url,
                cancellationToken: cancellationToken);

            if (response == null || response.Results == null || response.Results.Length == 0)
            {
                _logger.LogWarning("No options contracts found for {Symbol}", symbol);
                return new OptionChainSnapshot
                {
                    Symbol = symbol,
                    SpotPrice = spotPrice,
                    Timestamp = DateTime.UtcNow,
                    Contracts = Array.Empty<OptionContract>()
                };
            }

            // For each contract, get current quote
            var contracts = new List<OptionContract>();
            foreach (var contract in response.Results.Take(100)) // Limit for initial implementation
            {
                try
                {
                    var quote = await GetOptionQuoteAsync(contract.Ticker, cancellationToken);
                    contracts.Add(quote);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get quote for {Ticker}", contract.Ticker);
                }
            }

            _logger.LogInformation(
                "Retrieved chain with {Count} contracts for {Symbol}",
                contracts.Count, symbol);

            return new OptionChainSnapshot
            {
                Symbol = symbol,
                SpotPrice = spotPrice,
                Timestamp = DateTime.UtcNow,
                Contracts = contracts
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching option chain for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to fetch option chain for {symbol}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetSpotPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        // Get previous close: /v2/aggs/ticker/{ticker}/prev
        var url = $"/v2/aggs/ticker/{symbol}/prev?adjusted=true&apiKey={_apiKey}";

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
        CancellationToken cancellationToken = default)
    {
        // Calculate from last 30 days of bars
        var endDate = DateTime.UtcNow.Date;
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
        var url = $"/v3/snapshot/options/{underlying}/{optionTicker}?apiKey={_apiKey}";

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
    /// </summary>
    private static (string underlying, decimal strike, DateTime expiration, OptionRight right) 
        ParseOptionTicker(string ticker)
    {
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