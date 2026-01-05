// DTpr001A.cs - Polygon.io REST API client for market data

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Alaris.Core.Math;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Http.Contracts;

namespace Alaris.Infrastructure.Data.Provider.Polygon;

/// <summary>
/// Polygon.io REST API client for market data retrieval.
/// Component ID: DTpr001A
/// </summary>
/// <remarks>
/// <para>
/// Implements DTpr003A (Market Data Provider interface) using Polygon.io API
/// via Refit declarative interface (IPolygonApi).
/// </para>
/// <para>
/// Note: Polygon does NOT provide historical Greeks/IV via API.
/// This provider calculates IV from historical option prices using Black-Scholes,
/// which is the industry-standard approach for backtesting.
/// </para>
/// <para>
/// Resilience provided by Microsoft.Extensions.Http.Resilience standard handler.
/// </para>
/// </remarks>
public sealed class PolygonApiClient : DTpr003A
{
    private readonly IPolygonApi _api;
    private readonly ILogger<PolygonApiClient> _logger;
    private readonly string _apiKey;

    public PolygonApiClient(
        IPolygonApi api,
        IConfiguration configuration,
        ILogger<PolygonApiClient> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _apiKey = configuration["Polygon:ApiKey"] 
            ?? throw new InvalidOperationException("Polygon API key not configured");
            
        string maskedKey = _apiKey.Length > 4 ? _apiKey[..4] + new string('*', _apiKey.Length - 4) : "INVALID";
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

        DateTime referenceDate = endDate > DateTime.UtcNow.Date ? DateTime.UtcNow.Date : endDate;
        DateTime minAllowedDate = referenceDate.AddYears(-2).Date;
        if (startDate < minAllowedDate)
        {
            _logger.LogError("Date range outside 2-year subscription limit. Start Date {StartDate:yyyy-MM-dd} is older than {MinAllowedDate:yyyy-MM-dd}", 
                startDate, minAllowedDate);
            throw new ArgumentOutOfRangeException(nameof(startDate), 
                $"Date range outside 2-year subscription limit. Start Date {startDate:yyyy-MM-dd} is older than {minAllowedDate:yyyy-MM-dd}.");
        }

        try
        {
            PolygonAggregatesResponse response = await _api.GetDailyBarsAsync(
                symbol,
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                adjusted: true,
                sort: "asc",
                apiKey: _apiKey,
                cancellationToken);

            if (response?.Results == null || response.Results.Length == 0)
            {
                _logger.LogWarning("No data returned from Polygon for {Symbol}", symbol);
                return Array.Empty<PriceBar>();
            }

            List<PriceBar> bars = response.Results.Select(r => new PriceBar
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

        DateTime referenceDate = asOfDate > DateTime.UtcNow.Date ? DateTime.UtcNow.Date : asOfDate;
        DateTime twoYearsAgo = referenceDate.AddYears(-2).AddDays(1);
        DateTime effectiveDate = asOfDate < twoYearsAgo ? twoYearsAgo : asOfDate;
        
        if (asOfDate < twoYearsAgo)
        {
            _logger.LogWarning("asOfDate {Date:yyyy-MM-dd} is outside 2-year subscription limit, using {EffectiveDate:yyyy-MM-dd}", 
                asOfDate, effectiveDate);
        }

        _logger.LogInformation("Fetching historical option chain for {Symbol} as of {Date:yyyy-MM-dd}", symbol, effectiveDate);

        // 1. Get historical spot price
        decimal spotPrice = 0m;
        try
        {
            DateTime spotStart = effectiveDate.AddDays(-5);
            if (spotStart < twoYearsAgo) spotStart = twoYearsAgo;
            IReadOnlyList<PriceBar> bars = await GetHistoricalBarsAsync(symbol, spotStart, effectiveDate, cancellationToken);
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
        string dateStr = effectiveDate.ToString("yyyy-MM-dd");
        string expirationMin = effectiveDate.ToString("yyyy-MM-dd");
        string expirationMax = effectiveDate.AddDays(60).ToString("yyyy-MM-dd");
        
        _logger.LogDebug("Fetching reference contracts for {Symbol}", symbol);
        
        try
        {
            PolygonOptionsContractsResponse refResponse = await _api.GetOptionsContractsAsync(
                underlyingTicker: symbol,
                asOfDate: dateStr,
                expirationMin: expirationMin,
                expirationMax: expirationMax,
                limit: 250,
                apiKey: _apiKey,
                cancellationToken);
            
            if (refResponse?.Results == null || refResponse.Results.Length == 0)
            {
                _logger.LogWarning("No reference options found for {Symbol} as of {Date}", symbol, dateStr);
                return new OptionChainSnapshot { Symbol = symbol, SpotPrice = spotPrice, Timestamp = effectiveDate, Contracts = new List<OptionContract>() };
            }
            
            _logger.LogInformation("Found {Count} reference contracts for {Symbol}, fetching daily bars (max 50)...", refResponse.Results.Length, symbol);

            // 3. Fetch daily bars for each contract - WITH PARALLELISM and LIMITS
            PolygonOptionContract[] contractsToFetch = refResponse.Results.Take(50).ToArray();
            ConcurrentBag<OptionContract> contracts = new();
            bool subscriptionLimitHit = false;
            
            // Use semaphore to limit concurrent requests (Polygon rate limits apply)
            using SemaphoreSlim semaphore = new(5);
            IEnumerable<Task> tasks = contractsToFetch.Select(async refContract =>
            {
                if (subscriptionLimitHit)
                    return;
                    
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    PolygonAggregatesResponse aggResponse = await _api.GetTickerAggregatesAsync(
                        refContract.Ticker,
                        dateStr,
                        dateStr,
                        adjusted: true,
                        apiKey: _apiKey,
                        cancellationToken);
                    
                    if (aggResponse?.Results == null || aggResponse.Results.Length == 0)
                        return;

                    PolygonBar bar = aggResponse.Results[0];
                    
                    // Parse contract details from OCC ticker format
                    (string underlying, decimal strike, DateTime expiration, OptionRight right) = ParseOptionTicker(refContract.Ticker);
                    
                    // Calculate IV using CRMF001A Black-Scholes solver (industry standard for backtesting)
                    double riskFreeRate = 0.05;
                    double timeToExpiry = (expiration - effectiveDate).TotalDays / 365.25;
                    double impliedVol = CRMF001A.BSImpliedVolatility(
                        (double)spotPrice, (double)strike, timeToExpiry, riskFreeRate, 0.0,
                        (double)bar.Close, right == OptionRight.Call);
                    
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
                        ImpliedVolatility = !double.IsNaN(impliedVol) && impliedVol > 0 ? (decimal)impliedVol : null,
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(bar.Timestamp).DateTime
                    });
                }
                catch (OperationCanceledException)
                {
                    // Timeout - skip this contract
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning(
                        "Options data for {Date} is outside the 2-year historical data window. Skipping remaining contracts.",
                        dateStr);
                    subscriptionLimitHit = true;
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
            _logger.LogError(ex, "Error fetching historical option chain for {Symbol}", symbol);
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

        try
        {
            PolygonAggregatesResponse response = await _api.GetPreviousDayAsync(
                symbol,
                adjusted: true,
                apiKey: _apiKey,
                cancellationToken);

            if (response?.Results == null || response.Results.Length == 0)
                throw new InvalidOperationException($"No spot price data for {symbol}");

            decimal spotPrice = response.Results[0].Close;
            _logger.LogDebug("Spot price for {Symbol}: {Price}", symbol, spotPrice);

            return spotPrice;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spot price for {Symbol}", symbol);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        DateTime? evaluationDate = null,
        CancellationToken cancellationToken = default)
    {
        DateTime endDate = (evaluationDate ?? DateTime.UtcNow).Date;
        DateTime startDate = endDate.AddDays(-30);

        IReadOnlyList<PriceBar> bars = await GetHistoricalBarsAsync(symbol, startDate, endDate, cancellationToken);

        if (bars.Count == 0)
            throw new InvalidOperationException($"No historical data available for {symbol}");

        decimal avgVolume = (decimal)bars.Average(b => b.Volume);
        return avgVolume;
    }



    #region Option Ticker Parsing

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
            char c = ticker[i];
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
        
        char rightChar = ticker[cpIndex];
        string strikeStr = ticker[(cpIndex + 1)..];
        string dateStr = ticker[(cpIndex - 6)..cpIndex];
        string underlying = ticker[..(cpIndex - 6)];
        
        // Parse with 2-digit year prefix
        DateTime expiration = DateTime.ParseExact($"20{dateStr}", "yyyyMMdd", null);
        OptionRight right = rightChar == 'C' ? OptionRight.Call : OptionRight.Put;
        decimal strike = decimal.Parse(strikeStr) / 1000m;

        return (underlying, strike, expiration, right);
    }

    #endregion
}
