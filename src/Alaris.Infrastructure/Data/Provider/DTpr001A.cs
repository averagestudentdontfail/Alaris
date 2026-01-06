// DTpr001A.cs - Polygon.io REST API client for market data

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly int _maxConcurrentRequests;
    private readonly int _requestsPerSecond;
    private readonly int _optionsContractLimit;
    private readonly int _optionsContractsPerExpiryRight;
    private readonly int _optionsMaxExpirations;
    private readonly int _optionsChainParallelism;
    private readonly int _optionsChainDelayMs;
    private readonly int _optionsBootstrapStrideDays;
    private readonly int _optionsContractListCacheDays;
    private readonly OptionsRightFilter _optionsRightFilter;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly CancellationTokenSource _rateLimitCts = new();
    private readonly ConcurrentDictionary<string, ContractCacheEntry> _contractCache =
        new ConcurrentDictionary<string, ContractCacheEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _contractCacheLocks =
        new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

    public int OptionsChainParallelism => _optionsChainParallelism;
    public int OptionsChainDelayMs => _optionsChainDelayMs;
    public int OptionsBootstrapStrideDays => _optionsBootstrapStrideDays;

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

        _maxConcurrentRequests = Math.Max(1, configuration.GetValue("Polygon:MaxConcurrentRequests", 25));
        _requestsPerSecond = Math.Max(1, configuration.GetValue("Polygon:RequestsPerSecond", 100));
        _optionsContractLimit = Math.Max(1, configuration.GetValue("Polygon:OptionsContractLimit", 20));
        _optionsContractsPerExpiryRight = Math.Max(1, configuration.GetValue("Polygon:OptionsContractsPerExpiryRight", 2));
        _optionsMaxExpirations = Math.Max(1, configuration.GetValue("Polygon:OptionsMaxExpirations", 3));
        _optionsChainParallelism = Math.Max(1, configuration.GetValue("Polygon:OptionsChainParallelism", 4));
        _optionsChainDelayMs = Math.Max(0, configuration.GetValue("Polygon:OptionsChainDelayMs", 0));
        _optionsBootstrapStrideDays = Math.Max(1, configuration.GetValue("Polygon:OptionsBootstrapStrideDays", 1));
        _optionsContractListCacheDays = Math.Max(0, configuration.GetValue("Polygon:OptionsContractListCacheDays", 7));
        _optionsRightFilter = ParseOptionsRightFilter(configuration.GetValue("Polygon:OptionsRightFilter", "both"));

        _rateLimiter = new SemaphoreSlim(_requestsPerSecond, _requestsPerSecond);
        _ = Task.Run(async () => await RunRateLimitRefillAsync());
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
            await WaitForRateLimitAsync(cancellationToken);
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

            List<PriceBar> bars = new List<PriceBar>(response.Results.Length);
            foreach (PolygonBar result in response.Results)
            {
                PriceBar bar = new PriceBar
                {
                    Symbol = symbol,
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(result.Timestamp).DateTime,
                    Open = result.Open,
                    High = result.High,
                    Low = result.Low,
                    Close = result.Close,
                    Volume = (long)result.Volume
                };
                bars.Add(bar);
            }

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
    public Task<OptionChainSnapshot> GetHistoricalOptionChainAsync(
        string symbol,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        return GetHistoricalOptionChainAsync(symbol, asOfDate, null, cancellationToken);
    }

    /// <summary>
    /// Gets historical option chain for backtesting with optional spot price override.
    /// </summary>
    public async Task<OptionChainSnapshot> GetHistoricalOptionChainAsync(
        string symbol,
        DateTime asOfDate,
        decimal? spotPriceOverride,
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
        decimal spotPrice = spotPriceOverride.GetValueOrDefault();
        if (spotPrice <= 0m)
        {
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
            PolygonOptionContract[] refContracts = await GetReferenceContractsAsync(
                symbol,
                effectiveDate,
                expirationMin,
                expirationMax,
                cancellationToken);
            if (refContracts.Length == 0)
            {
                _logger.LogWarning("No reference options found for {Symbol} as of {Date}", symbol, dateStr);
                return new OptionChainSnapshot { Symbol = symbol, SpotPrice = spotPrice, Timestamp = effectiveDate, Contracts = new List<OptionContract>() };
            }
            
            IReadOnlyList<PolygonOptionContract> contractsToFetch = SelectContractsForSnapshot(refContracts, spotPrice, effectiveDate);
            if (contractsToFetch.Count == 0)
            {
                _logger.LogWarning("No eligible options found for {Symbol} as of {Date}", symbol, dateStr);
                return new OptionChainSnapshot { Symbol = symbol, SpotPrice = spotPrice, Timestamp = effectiveDate, Contracts = new List<OptionContract>() };
            }

            _logger.LogInformation(
                "Found {Count} reference contracts for {Symbol}, fetching daily bars for {Selected}...",
                refContracts.Length,
                symbol,
                contractsToFetch.Count);

            // 3. Fetch daily bars for each contract - WITH PARALLELISM and LIMITS
            ConcurrentBag<OptionContract> contracts = new ConcurrentBag<OptionContract>();
            volatile bool subscriptionLimitHit = false;
            
            // Use semaphore to limit concurrent requests (Polygon rate limits apply)
            using SemaphoreSlim semaphore = new(_maxConcurrentRequests);
            List<Task> tasks = new List<Task>(contractsToFetch.Count);
            for (int i = 0; i < contractsToFetch.Count; i++)
            {
                PolygonOptionContract refContract = contractsToFetch[i];
                tasks.Add(FetchContractAsync(refContract));
            }
            
            await Task.WhenAll(tasks);

            async Task FetchContractAsync(PolygonOptionContract refContract)
            {
                if (subscriptionLimitHit)
                {
                    return;
                }
                    
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await WaitForRateLimitAsync(cancellationToken);
                    PolygonAggregatesResponse aggResponse = await _api.GetTickerAggregatesAsync(
                        refContract.Ticker,
                        dateStr,
                        dateStr,
                        adjusted: true,
                        apiKey: _apiKey,
                        cancellationToken);
                    
                    if (aggResponse?.Results == null || aggResponse.Results.Length == 0)
                    {
                        return;
                    }

                    PolygonBar bar = aggResponse.Results[0];
                    
                    // Parse contract details from OCC ticker format
                    (string underlying, decimal strike, DateTime expiration, OptionRight right) = ParseOptionTicker(refContract.Ticker);
                    
                    // Calculate IV using CRMF001A Black-Scholes solver (industry standard for backtesting)
                    double riskFreeRate = 0.05;
                    double timeToExpiry = (expiration - effectiveDate).TotalDays / 365.25;
                    double impliedVol = CRMF001A.BSImpliedVolatility(
                        (double)spotPrice, (double)strike, timeToExpiry, riskFreeRate, 0.0,
                        (double)bar.Close, right == OptionRight.Call);
                    
                    OptionContract contract = new OptionContract
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
                    };
                    
                    contracts.Add(contract);
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
            }

            _logger.LogInformation("Retrieved {Count} contracts with pricing for {Symbol}", contracts.Count, symbol);

            return new OptionChainSnapshot
            {
                Symbol = symbol,
                SpotPrice = spotPrice,
                Timestamp = effectiveDate,
                Contracts = ToContractList(contracts)
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
            await WaitForRateLimitAsync(cancellationToken);
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

        decimal sumVolume = 0m;
        for (int i = 0; i < bars.Count; i++)
        {
            sumVolume += bars[i].Volume;
        }
        decimal avgVolume = sumVolume / bars.Count;
        return avgVolume;
    }

    private static List<OptionContract> ToContractList(IEnumerable<OptionContract> contracts)
    {
        List<OptionContract> list = new List<OptionContract>();
        foreach (OptionContract contract in contracts)
        {
            list.Add(contract);
        }
        return list;
    }

    private IReadOnlyList<PolygonOptionContract> SelectContractsForSnapshot(
        IReadOnlyList<PolygonOptionContract> contracts,
        decimal spotPrice,
        DateTime effectiveDate)
    {
        if (contracts.Count == 0)
        {
            return Array.Empty<PolygonOptionContract>();
        }

        int limit = Math.Min(_optionsContractLimit, contracts.Count);
        if (spotPrice <= 0m || _optionsContractsPerExpiryRight <= 0 || _optionsMaxExpirations <= 0)
        {
            return contracts.Take(limit).ToList();
        }

        List<(PolygonOptionContract Contract, DateTime Expiration, string? Type)> parsed =
            new List<(PolygonOptionContract, DateTime, string?)>(contracts.Count);

        for (int i = 0; i < contracts.Count; i++)
        {
            PolygonOptionContract contract = contracts[i];
            if (!TryParseExpiration(contract, out DateTime expiration))
            {
                continue;
            }

            if (expiration.Date < effectiveDate.Date)
            {
                continue;
            }

            string? type = NormalizeContractType(contract);
            if (!IsRightAllowed(type, contract))
            {
                continue;
            }

            parsed.Add((contract, expiration.Date, type));
        }

        if (parsed.Count == 0)
        {
            return contracts.Take(limit).ToList();
        }

        List<PolygonOptionContract> selected = new List<PolygonOptionContract>(limit);
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int expirationsAdded = 0;
        foreach (IGrouping<DateTime, (PolygonOptionContract Contract, DateTime Expiration, string? Type)> expiryGroup in parsed
            .GroupBy(p => p.Expiration)
            .OrderBy(g => g.Key))
        {
            if (expirationsAdded >= _optionsMaxExpirations || selected.Count >= limit)
            {
                break;
            }

            foreach (string right in GetRightOrder())
            {
                foreach ((PolygonOptionContract Contract, DateTime Expiration, string? Type) candidate in expiryGroup
                    .Where(p => string.Equals(p.Type, right, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => Math.Abs(p.Contract.StrikePrice - spotPrice))
                    .Take(_optionsContractsPerExpiryRight))
                {
                    if (selected.Count >= limit)
                    {
                        break;
                    }

                    if (seen.Add(candidate.Contract.Ticker))
                    {
                        selected.Add(candidate.Contract);
                    }
                }
            }

            expirationsAdded++;
        }

        if (selected.Count < limit)
        {
            foreach ((PolygonOptionContract Contract, DateTime Expiration, string? Type) candidate in parsed
                .OrderBy(p => Math.Abs(p.Contract.StrikePrice - spotPrice)))
            {
                if (selected.Count >= limit)
                {
                    break;
                }

                if (seen.Add(candidate.Contract.Ticker))
                {
                    selected.Add(candidate.Contract);
                }
            }
        }

        return selected.Count > 0 ? selected : contracts.Take(limit).ToList();
    }

    private static bool TryParseExpiration(PolygonOptionContract contract, out DateTime expiration)
    {
        if (!string.IsNullOrWhiteSpace(contract.ExpirationDate) &&
            DateTime.TryParseExact(
                contract.ExpirationDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out expiration))
        {
            return true;
        }

        try
        {
            (_, _, expiration, _) = ParseOptionTicker(contract.Ticker);
            return true;
        }
        catch (FormatException)
        {
            expiration = default;
            return false;
        }
    }

    private async Task<PolygonOptionContract[]> GetReferenceContractsAsync(
        string symbol,
        DateTime asOfDate,
        string expirationMin,
        string expirationMax,
        CancellationToken cancellationToken)
    {
        if (_optionsContractListCacheDays > 0 &&
            TryGetCachedContracts(symbol, asOfDate, out PolygonOptionContract[] cached))
        {
            return cached;
        }

        SemaphoreSlim cacheLock = _contractCacheLocks.GetOrAdd(symbol, _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_optionsContractListCacheDays > 0 &&
                TryGetCachedContracts(symbol, asOfDate, out cached))
            {
                return cached;
            }

            await WaitForRateLimitAsync(cancellationToken);
            PolygonOptionsContractsResponse refResponse = await _api.GetOptionsContractsAsync(
                underlyingTicker: symbol,
                asOfDate: asOfDate.ToString("yyyy-MM-dd"),
                expirationMin: expirationMin,
                expirationMax: expirationMax,
                limit: 250,
                apiKey: _apiKey,
                cancellationToken);

            PolygonOptionContract[] results = refResponse?.Results ?? Array.Empty<PolygonOptionContract>();
            if (_optionsContractListCacheDays > 0 && results.Length > 0)
            {
                _contractCache[symbol] = new ContractCacheEntry
                {
                    AsOfDate = asOfDate.Date,
                    Contracts = results
                };
            }

            return results;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private bool TryGetCachedContracts(string symbol, DateTime asOfDate, out PolygonOptionContract[] contracts)
    {
        contracts = Array.Empty<PolygonOptionContract>();
        if (_optionsContractListCacheDays <= 0)
        {
            return false;
        }

        if (_contractCache.TryGetValue(symbol, out ContractCacheEntry entry))
        {
            if (entry.AsOfDate <= asOfDate.Date &&
                (asOfDate.Date - entry.AsOfDate).TotalDays <= _optionsContractListCacheDays)
            {
                contracts = entry.Contracts;
                return true;
            }
        }

        return false;
    }

    private string? NormalizeContractType(PolygonOptionContract contract)
    {
        string? type = contract.ContractType;
        if (!string.IsNullOrWhiteSpace(type))
        {
            string normalized = type.ToLowerInvariant();
            if (normalized == "c")
            {
                return "call";
            }
            if (normalized == "p")
            {
                return "put";
            }
            return normalized;
        }

        if (TryGetContractTypeFromTicker(contract.Ticker, out string? parsed))
        {
            return parsed;
        }

        return null;
    }

    private bool IsRightAllowed(string? contractType, PolygonOptionContract contract)
    {
        if (_optionsRightFilter == OptionsRightFilter.Both)
        {
            return true;
        }

        string? type = contractType;
        if (string.IsNullOrWhiteSpace(type) && TryGetContractTypeFromTicker(contract.Ticker, out string? parsed))
        {
            type = parsed;
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        return _optionsRightFilter == OptionsRightFilter.Call
            ? string.Equals(type, "call", StringComparison.OrdinalIgnoreCase)
            : string.Equals(type, "put", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetContractTypeFromTicker(string ticker, out string? type)
    {
        try
        {
            (_, _, _, OptionRight right) = ParseOptionTicker(ticker);
            type = right == OptionRight.Call ? "call" : "put";
            return true;
        }
        catch (FormatException)
        {
            type = null;
            return false;
        }
    }

    private string[] GetRightOrder()
    {
        return _optionsRightFilter switch
        {
            OptionsRightFilter.Call => new[] { "call" },
            OptionsRightFilter.Put => new[] { "put" },
            _ => new[] { "call", "put" }
        };
    }

    private static OptionsRightFilter ParseOptionsRightFilter(string? value)
    {
        if (string.Equals(value, "call", StringComparison.OrdinalIgnoreCase))
        {
            return OptionsRightFilter.Call;
        }
        if (string.Equals(value, "put", StringComparison.OrdinalIgnoreCase))
        {
            return OptionsRightFilter.Put;
        }
        return OptionsRightFilter.Both;
    }

    private async Task RunRateLimitRefillAsync()
    {
        using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_rateLimitCts.Token))
            {
                int toRelease = _requestsPerSecond - _rateLimiter.CurrentCount;
                if (toRelease > 0)
                {
                    _rateLimiter.Release(toRelease);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private ValueTask WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        return new ValueTask(_rateLimiter.WaitAsync(cancellationToken));
    }


    private enum OptionsRightFilter
    {
        Both,
        Call,
        Put
    }

    private sealed class ContractCacheEntry
    {
        public required DateTime AsOfDate { get; init; }
        public required PolygonOptionContract[] Contracts { get; init; }
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
                i + 9 == ticker.Length) // C/P + 8 strike digits = end of string
            {
                bool digitsOnly = true;
                for (int j = i + 1; j < ticker.Length; j++)
                {
                    if (!char.IsDigit(ticker[j]))
                    {
                        digitsOnly = false;
                        break;
                    }
                }

                if (digitsOnly)
                {
                    cpIndex = i;
                    break;
                }
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
