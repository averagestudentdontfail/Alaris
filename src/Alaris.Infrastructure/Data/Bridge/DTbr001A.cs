using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Provider;
using Alaris.Infrastructure.Data.Quality;
using Alaris.Infrastructure.Data.Serialization;
using Alaris.Infrastructure.Protocol.Buffers;

namespace Alaris.Infrastructure.Data.Bridge;

/// <summary>
/// Bridges Alaris data providers to Lean engine's data infrastructure.
/// Component ID: DTbr001A
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Aggregates data from multiple providers (Polygon, IBKR)
/// - Runs data quality validation pipeline
/// - Provides unified MarketDataSnapshot for strategy evaluation
/// - Integrates with Lean's SubscriptionDataSource when needed
/// 
/// Usage in Lean algorithm:
/// var bridge = new AlarisDataBridge(providers, validators, logger);
/// var snapshot = await bridge.GetMarketDataSnapshotAsync("AAPL");
/// </remarks>
public sealed class AlarisDataBridge
{
    private readonly DTpr003A? _marketDataProvider;
    private readonly DTpr004A _earningsProvider;
    private readonly DTpr005A _riskFreeRateProvider;
    private readonly IReadOnlyList<DTqc002A> _validators;
    private readonly ILogger<AlarisDataBridge> _logger;
    private bool _allowOptionChainFallback = true;
    private bool _allowEarningsFallback = true;
    
    // Session data path for loading cached data (options, etc.)
    private string? _sessionDataPath;
    private static readonly JsonSerializerOptions JsonOptions =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="AlarisDataBridge"/> class.
    /// </summary>
    /// <param name="marketDataProvider">Market data provider (Polygon). Can be null in backtest mode with cached data.</param>
    /// <param name="earningsProvider">Earnings calendar provider.</param>
    /// <param name="riskFreeRateProvider">Risk-free rate provider.</param>
    /// <param name="validators">Data quality validators.</param>
    /// <param name="logger">Logger instance.</param>
    public AlarisDataBridge(
        DTpr003A? marketDataProvider,
        DTpr004A earningsProvider,
        DTpr005A riskFreeRateProvider,
        IReadOnlyList<DTqc002A> validators,
        ILogger<AlarisDataBridge> logger)
    {
        _marketDataProvider = marketDataProvider; // Can be null for backtest with cached data
        _earningsProvider = earningsProvider ?? throw new ArgumentNullException(nameof(earningsProvider));
        _riskFreeRateProvider = riskFreeRateProvider ?? throw new ArgumentNullException(nameof(riskFreeRateProvider));
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the market data provider or throws if not available.
    /// Used for operations that require live API access.
    /// </summary>
    private DTpr003A RequireMarketDataProvider()
    {
        return _marketDataProvider ?? throw new InvalidOperationException(
            "Market data provider is not available. In backtest mode with cached data, " +
            "ensure all required data is pre-downloaded or configure the Polygon API key.");
    }

    /// <summary>
    /// Sets the session data path for loading cached data (options, historical bars, etc.).
    /// Call this before using the bridge in backtest mode.
    /// </summary>
    /// <param name="sessionDataPath">Path to the session's data folder.</param>
    public void SetSessionDataPath(string sessionDataPath)
    {
        _sessionDataPath = sessionDataPath;
        _logger.LogInformation("Session data path set to: {Path}", sessionDataPath);
    }

    /// <summary>
    /// Enables or disables live option chain fallback when cache is missing.
    /// </summary>
    public void SetOptionChainFallbackEnabled(bool enabled)
    {
        _allowOptionChainFallback = enabled;
    }

    /// <summary>
    /// Enables or disables live earnings fallback when cache is missing.
    /// </summary>
    public void SetEarningsFallbackEnabled(bool enabled)
    {
        _allowEarningsFallback = enabled;
    }

    /// <summary>
    /// Gets complete market data snapshot for strategy evaluation.
    /// </summary>
    /// <param name="symbol">The symbol to evaluate.</param>
    /// <param name="evaluationDate">The date for data evaluation (use LEAN's simulation time for backtests).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validated market data snapshot.</returns>
    /// <exception cref="InvalidOperationException">If data quality validation fails.</exception>
    public async Task<MarketDataSnapshot> GetMarketDataSnapshotAsync(
        string symbol,
        DateTime? evaluationDate = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        // Use provided date or fall back to UTC now for live trading
        DateTime effectiveDate = evaluationDate ?? DateTime.UtcNow;
        _logger.LogInformation("Building market data snapshot for {Symbol} as of {Date:yyyy-MM-dd}", symbol, effectiveDate);

        try
        {
            // Step 1: Fetch primary data concurrently
            // Note: Spot price is derived from historical bars, NOT from /prev endpoint
            // The /prev endpoint only returns yesterday's close and doesn't work for historical dates in backtests
            Task<IReadOnlyList<PriceBar>> historicalBarsTask =
                GetHistoricalBarsForRvCalculationAsync(symbol, effectiveDate, cancellationToken);
            Task<OptionChainSnapshot> optionChainTask =
                GetOptionChainWithCacheFallbackAsync(symbol, effectiveDate, cancellationToken);
            Task<(EarningsEvent? next, IReadOnlyList<EarningsEvent> historical)> earningsTask =
                GetEarningsDataAsync(symbol, effectiveDate, cancellationToken);
            
            // Note: Average volume will be computed from historical bars (cache-friendly)
            // rather than making a separate API call

            await Task.WhenAll(
                historicalBarsTask,
                optionChainTask,
                earningsTask);

            IReadOnlyList<PriceBar> historicalBars = await historicalBarsTask;
            OptionChainSnapshot optionChain = await optionChainTask;
            (EarningsEvent? nextEarnings, IReadOnlyList<EarningsEvent> historicalEarnings) = await earningsTask;
            
            // Compute average volume from historical bars (avoids separate API call)
            decimal avgVolume = ComputeAverageVolumeFromBars(historicalBars, symbol, effectiveDate);
            
            // Derive spot price from the most recent bar's close price
            // This works for both live (recent bars) and backtesting (historical bars)
            decimal spotPrice;
            if (historicalBars.Count > 0)
            {
                // Get the bar closest to evaluation date (but not after it)
                PriceBar? relevantBar = null;
                for (int i = 0; i < historicalBars.Count; i++)
                {
                    PriceBar bar = historicalBars[i];
                    if (bar.Timestamp.Date > effectiveDate.Date)
                    {
                        continue;
                    }

                    if (relevantBar == null || bar.Timestamp > relevantBar.Timestamp)
                    {
                        relevantBar = bar;
                    }
                }
                    
                spotPrice = relevantBar?.Close ?? historicalBars[^1].Close;
                _logger.LogDebug("Derived spot price {Price} from historical bars for {Symbol}", spotPrice, symbol);
            }
            else
            {
                // Fallback to live spot price only if no historical data
                spotPrice = await RequireMarketDataProvider().GetSpotPriceAsync(symbol, cancellationToken);
            }
            // Get risk-free rate from cache first (backtest mode), then live API
            decimal riskFreeRate;
            decimal? cachedRate = GetRiskFreeRateFromCache(effectiveDate);
            
            if (cachedRate.HasValue && cachedRate.Value > 0)
            {
                riskFreeRate = cachedRate.Value;
                _logger.LogDebug("Using cached risk-free rate {Rate:P4} for {Date:yyyy-MM-dd}", 
                    riskFreeRate, effectiveDate);
            }
            else
            {
                // Try live API if no cache
                try
                {
                    riskFreeRate = await _riskFreeRateProvider.GetCurrentRateAsync(cancellationToken);
                    if (riskFreeRate <= 0)
                    {
                        riskFreeRate = 0.045m; // Fallback: typical 2024 rate
                        _logger.LogDebug("Treasury returned 0%, using fallback rate {Rate:P4}", riskFreeRate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch risk-free rate, using fallback");
                    riskFreeRate = 0.045m; // Fallback: typical 2024 rate
                }
            }

            decimal dividendYield = EstimateDividendYield(optionChain, spotPrice, riskFreeRate, effectiveDate);

            // Step 2: Construct snapshot
            MarketDataSnapshot snapshot = new MarketDataSnapshot
            {
                Symbol = symbol,
                Timestamp = effectiveDate,
                SpotPrice = spotPrice,
                HistoricalBars = historicalBars,
                OptionChain = optionChain,
                NextEarnings = nextEarnings,
                HistoricalEarnings = historicalEarnings,
                RiskFreeRate = riskFreeRate,
                DividendYield = dividendYield,
                AverageVolume30Day = avgVolume
            };

            // Step 3: Run data quality validation
            IReadOnlyList<DataQualityResult> validationResults = RunDataQualityValidation(snapshot);

            // Step 4: Check for critical failures
            List<DataQualityResult> criticalFailures = new List<DataQualityResult>();
            foreach (DataQualityResult result in validationResults)
            {
                if (result.Status == ValidationStatus.Failed)
                {
                    criticalFailures.Add(result);
                }
            }

            if (criticalFailures.Count > 0)
            {
                List<string> errorMessages = new List<string>(criticalFailures.Count);
                foreach (DataQualityResult failure in criticalFailures)
                {
                    errorMessages.Add(failure.Message);
                }
                string errors = string.Join("; ", errorMessages);
                _logger.LogError(
                    "Data quality validation failed for {Symbol}: {Errors}",
                    symbol, errors);

                throw new InvalidOperationException(
                    $"Data quality validation failed: {errors}");
            }

            // Step 5: Log warnings
            List<string> warnings = new List<string>();
            foreach (DataQualityResult result in validationResults)
            {
                if (result.Status != ValidationStatus.PassedWithWarnings)
                {
                    continue;
                }

                if (result.Warnings == null)
                {
                    continue;
                }

                foreach (string warning in result.Warnings)
                {
                    warnings.Add(warning);
                }
            }

            if (warnings.Count > 0)
            {
                _logger.LogWarning(
                    "Data quality warnings for {Symbol}: {Warnings}",
                    symbol, string.Join("; ", warnings));
            }

            _logger.LogInformation(
                "Successfully built market data snapshot for {Symbol} " +
                "(Bars: {BarCount}, Options: {OptionCount}, Earnings: {EarningsCount})",
                symbol,
                historicalBars.Count,
                optionChain.Contracts.Count,
                historicalEarnings.Count);

            return snapshot;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to build market data snapshot for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to build market data snapshot for {symbol}", ex);
        }
    }

    private decimal EstimateDividendYield(
        OptionChainSnapshot optionChain,
        decimal spotPrice,
        decimal riskFreeRate,
        DateTime evaluationDate)
    {
        if (spotPrice <= 0m)
        {
            _logger.LogWarning("Spot price unavailable for dividend yield estimate for {Symbol}", optionChain.Symbol);
            return 0m;
        }

        IReadOnlyList<OptionContract> contracts = optionChain.Contracts;
        if (contracts.Count == 0)
        {
            _logger.LogWarning("No option contracts available for dividend yield estimate for {Symbol}", optionChain.Symbol);
            return 0m;
        }

        DateTime effectiveDate = evaluationDate.Date;
        Dictionary<DateTime, List<OptionContract>> contractsByExpiration =
            BuildContractsByExpiration(contracts, effectiveDate);

        if (contractsByExpiration.Count == 0)
        {
            _logger.LogWarning("No future expirations available for dividend yield estimate for {Symbol}", optionChain.Symbol);
            return 0m;
        }

        List<DateTime> expirations = GetSortedExpirations(contractsByExpiration);

        for (int i = 0; i < expirations.Count; i++)
        {
            DateTime expiration = expirations[i];
            List<OptionContract> bucket = contractsByExpiration[expiration];

            if (!TrySelectAtmPair(bucket, spotPrice, out OptionContract call, out OptionContract put, out decimal strike))
            {
                continue;
            }

            if (!TryComputeImpliedDividendYield(
                    call,
                    put,
                    strike,
                    spotPrice,
                    riskFreeRate,
                    effectiveDate,
                    expiration,
                    out decimal impliedDividendYield))
            {
                continue;
            }

            if (impliedDividendYield < 0m)
            {
                _logger.LogWarning(
                    "Implied dividend yield is negative for {Symbol} ({Yield:P2}); using 0",
                    optionChain.Symbol,
                    impliedDividendYield);
                return 0m;
            }

            return impliedDividendYield;
        }

        _logger.LogWarning("Unable to estimate dividend yield for {Symbol}; using 0", optionChain.Symbol);
        return 0m;
    }

    private static Dictionary<DateTime, List<OptionContract>> BuildContractsByExpiration(
        IReadOnlyList<OptionContract> contracts,
        DateTime effectiveDate)
    {
        Dictionary<DateTime, List<OptionContract>> contractsByExpiration =
            new Dictionary<DateTime, List<OptionContract>>();

        for (int i = 0; i < contracts.Count; i++)
        {
            OptionContract contract = contracts[i];
            DateTime expiration = contract.Expiration.Date;
            if (expiration <= effectiveDate)
            {
                continue;
            }

            if (!contractsByExpiration.TryGetValue(expiration, out List<OptionContract>? bucket))
            {
                bucket = new List<OptionContract>();
                contractsByExpiration.Add(expiration, bucket);
            }

            bucket.Add(contract);
        }

        return contractsByExpiration;
    }

    private static List<DateTime> GetSortedExpirations(Dictionary<DateTime, List<OptionContract>> contractsByExpiration)
    {
        List<DateTime> expirations = new List<DateTime>(contractsByExpiration.Count);
        foreach (DateTime expiration in contractsByExpiration.Keys)
        {
            expirations.Add(expiration);
        }

        expirations.Sort();
        return expirations;
    }

    private static bool TrySelectAtmPair(
        List<OptionContract> bucket,
        decimal spotPrice,
        out OptionContract call,
        out OptionContract put,
        out decimal strike)
    {
        Dictionary<decimal, OptionContract> calls = new Dictionary<decimal, OptionContract>();
        Dictionary<decimal, OptionContract> puts = new Dictionary<decimal, OptionContract>();

        for (int i = 0; i < bucket.Count; i++)
        {
            OptionContract contract = bucket[i];
            if (contract.Right == OptionRight.Call)
            {
                calls.TryAdd(contract.Strike, contract);
            }
            else if (contract.Right == OptionRight.Put)
            {
                puts.TryAdd(contract.Strike, contract);
            }
        }

        call = null!;
        put = null!;
        strike = 0m;

        if (calls.Count == 0 || puts.Count == 0)
        {
            return false;
        }

        decimal bestDistance = decimal.MaxValue;
        foreach (KeyValuePair<decimal, OptionContract> callEntry in calls)
        {
            decimal candidateStrike = callEntry.Key;
            if (!puts.TryGetValue(candidateStrike, out OptionContract? putCandidate))
            {
                continue;
            }

            decimal distance = Math.Abs(candidateStrike - spotPrice);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                strike = candidateStrike;
                call = callEntry.Value;
                put = putCandidate;
            }
        }

        return bestDistance != decimal.MaxValue;
    }

    private static bool TryComputeImpliedDividendYield(
        OptionContract call,
        OptionContract put,
        decimal strike,
        decimal spotPrice,
        decimal riskFreeRate,
        DateTime effectiveDate,
        DateTime expiration,
        out decimal impliedDividendYield)
    {
        impliedDividendYield = 0m;

        decimal callMid = (call.Bid + call.Ask) / 2m;
        decimal putMid = (put.Bid + put.Ask) / 2m;
        if (callMid <= 0m || putMid <= 0m)
        {
            return false;
        }

        double timeToExpiry = (expiration - effectiveDate).TotalDays / 365.0;
        if (timeToExpiry <= 0.0)
        {
            return false;
        }

        double spot = (double)spotPrice;
        double strikeValue = (double)strike;
        double callValue = (double)callMid;
        double putValue = (double)putMid;
        double riskFree = (double)riskFreeRate;

        double discountedStrike = strikeValue * Math.Exp(-riskFree * timeToExpiry);
        double forward = callValue - putValue + discountedStrike;
        if (forward <= 0.0 || spot <= 0.0)
        {
            return false;
        }

        double ratio = forward / spot;
        if (ratio <= 0.0)
        {
            return false;
        }

        double implied = -Math.Log(ratio) / timeToExpiry;
        if (double.IsNaN(implied) || double.IsInfinity(implied))
        {
            return false;
        }

        impliedDividendYield = (decimal)implied;
        return true;
    }

    /// <summary>
    /// Gets an option chain snapshot for a given symbol and evaluation date.
    /// Uses session cache when configured.
    /// </summary>
    public Task<OptionChainSnapshot> GetOptionChainSnapshotAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken = default)
    {
        return GetOptionChainWithCacheFallbackAsync(symbol, evaluationDate, cancellationToken);
    }

    /// <summary>
    /// Gets option chain with cache fallback for backtest mode.
    /// Loads from session cache if available, otherwise falls back to live API.
    /// Supports three cache formats:
    ///   1. Date-specific: {symbol}_{yyyyMMdd}.sbe/.json (preferred for multi-date backtests)
    ///   2. Single-file binary: {symbol}.sbe
    ///   3. Single-file JSON: {symbol}.json (legacy)
    /// When exact date match is not found, uses the nearest earlier cached date.
    /// </summary>
    private async Task<OptionChainSnapshot> GetOptionChainWithCacheFallbackAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        // Try to load from session cache first (backtest mode)
        if (!string.IsNullOrEmpty(_sessionDataPath))
        {
            string optionsDir = System.IO.Path.Combine(_sessionDataPath, "options");
            string symbolLower = symbol.ToLowerInvariant();
            
            // Strategy 1: Try exact date-specific cache files (from earnings-based bootstrap)
            string dateSuffix = evaluationDate.ToString("yyyyMMdd");
            
            string dateSpecificBinaryPath = System.IO.Path.Combine(optionsDir, $"{symbolLower}_{dateSuffix}.sbe");
            if (File.Exists(dateSpecificBinaryPath))
            {
                try
                {
                    OptionChainSnapshot? cached = await LoadBinaryCacheAsync(dateSpecificBinaryPath, cancellationToken);
                    if (cached != null && cached.Contracts.Count > 0)
                    {
                        _logger.LogDebug("Loaded {Count} options from date-specific binary cache for {Symbol} @ {Date}", 
                            cached.Contracts.Count, symbol, evaluationDate);
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load date-specific binary cache for {Symbol}", symbol);
                }
            }
            
            string dateSpecificJsonPath = System.IO.Path.Combine(optionsDir, $"{symbolLower}_{dateSuffix}.json");
            if (File.Exists(dateSpecificJsonPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(dateSpecificJsonPath, cancellationToken);
                    OptionChainSnapshot? cached = JsonSerializer.Deserialize<OptionChainSnapshot>(json, JsonOptions);
                    if (cached != null && cached.Contracts.Count > 0)
                    {
                        _logger.LogDebug("Loaded {Count} options from date-specific JSON cache for {Symbol} @ {Date}", 
                            cached.Contracts.Count, symbol, evaluationDate);
                        return cached;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize date-specific JSON cache for {Symbol}", symbol);
                }
            }
            
            // Strategy 2: Find nearest earlier dated cache file
            if (Directory.Exists(optionsDir))
            {
                OptionChainSnapshot? nearestCache = await FindNearestCacheAsync(optionsDir, symbolLower, evaluationDate, cancellationToken);
                if (nearestCache != null)
                {
                    return nearestCache;
                }
            }
            
            // Strategy 3: Fall back to single-file cache (legacy format - date-agnostic)
            string binaryCachePath = System.IO.Path.Combine(optionsDir, $"{symbolLower}.sbe");
            if (File.Exists(binaryCachePath))
            {
                try
                {
                    OptionChainSnapshot? cached = await LoadBinaryCacheAsync(binaryCachePath, cancellationToken);
                    if (cached != null && cached.Contracts.Count > 0)
                    {
                        _logger.LogDebug("Loaded {Count} options from legacy binary cache for {Symbol} (using as fallback)", 
                            cached.Contracts.Count, symbol);
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load legacy binary cache for {Symbol}", symbol);
                }
            }
            
            string jsonCachePath = System.IO.Path.Combine(optionsDir, $"{symbolLower}.json");
            if (File.Exists(jsonCachePath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(jsonCachePath, cancellationToken);
                    OptionChainSnapshot? cached = JsonSerializer.Deserialize<OptionChainSnapshot>(json, JsonOptions);
                    
                    if (cached != null && cached.Contracts.Count > 0)
                    {
                        _logger.LogDebug("Loaded {Count} options from legacy JSON cache for {Symbol} (using as fallback)", 
                            cached.Contracts.Count, symbol);
                        return cached;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize legacy JSON cache for {Symbol}", symbol);
                }
            }
            
            _logger.LogDebug("No options cache found for {Symbol} @ {Date}", symbol, evaluationDate);
        }

        if (!_allowOptionChainFallback)
        {
            _logger.LogDebug("Option chain fallback disabled for {Symbol} @ {Date}", symbol, evaluationDate);
            return new OptionChainSnapshot
            {
                Symbol = symbol,
                SpotPrice = 0m,
                Timestamp = evaluationDate,
                Contracts = Array.Empty<OptionContract>()
            };
        }

        // Fetch from provider (Live or Historical On-Demand)
        _logger.LogDebug("Fetching option chain for {Symbol} as of {Date}", symbol, evaluationDate);
        return await RequireMarketDataProvider().GetOptionChainAsync(symbol, evaluationDate, cancellationToken);
    }
    
    /// <summary>
    /// Finds the nearest earlier cached options file for a symbol.
    /// </summary>
    private async Task<OptionChainSnapshot?> FindNearestCacheAsync(
        string optionsDir,
        string symbolLower,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find all cache files for this symbol with date suffixes
            string pattern = $"{symbolLower}_*.sbe";
            string[] binaryFiles = Directory.GetFiles(optionsDir, pattern);
            string jsonPattern = $"{symbolLower}_*.json";
            string[] jsonFiles = Directory.GetFiles(optionsDir, jsonPattern);
            
            // Parse dates from filenames and find nearest earlier date
            List<(DateTime date, string path, bool isBinary)> availableDates =
                new List<(DateTime date, string path, bool isBinary)>();
            
            foreach (string file in binaryFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string datePart = filename.Substring(symbolLower.Length + 1);
                if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    availableDates.Add((date, file, true));
                }
            }
            
            foreach (string file in jsonFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string datePart = filename.Substring(symbolLower.Length + 1);
                if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    // Only add if not already in list (binary takes precedence)
                    bool exists = false;
                    for (int i = 0; i < availableDates.Count; i++)
                    {
                        if (availableDates[i].date == date)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        availableDates.Add((date, file, false));
                    }
                }
            }
            
            if (availableDates.Count == 0)
            {
                return null;
            }
            
            // Find nearest date <= evaluationDate, or if none, nearest date > evaluationDate
            bool hasEarlier = false;
            DateTime nearestEarlierDate = DateTime.MinValue;
            (DateTime date, string path, bool isBinary) selected = availableDates[0];

            for (int i = 0; i < availableDates.Count; i++)
            {
                (DateTime date, string path, bool isBinary) entry = availableDates[i];
                if (entry.date <= evaluationDate)
                {
                    if (!hasEarlier || entry.date > nearestEarlierDate)
                    {
                        hasEarlier = true;
                        nearestEarlierDate = entry.date;
                        selected = entry;
                    }
                }
            }

            if (!hasEarlier)
            {
                DateTime earliestDate = availableDates[0].date;
                selected = availableDates[0];
                for (int i = 1; i < availableDates.Count; i++)
                {
                    if (availableDates[i].date < earliestDate)
                    {
                        earliestDate = availableDates[i].date;
                        selected = availableDates[i];
                    }
                }
            }
            
            _logger.LogDebug("Using cached options from {CacheDate} for {EvalDate} (delta: {Days} days)", 
                selected.date, evaluationDate, Math.Abs((evaluationDate - selected.date).Days));
            
            if (selected.isBinary)
            {
                return await LoadBinaryCacheAsync(selected.path, cancellationToken);
            }
            else
            {
                string json = await File.ReadAllTextAsync(selected.path, cancellationToken);
                return JsonSerializer.Deserialize<OptionChainSnapshot>(json, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding nearest cache for {Symbol}", symbolLower);
            return null;
        }
    }

    /// <summary>
    /// Loads option chain snapshot from binary cache file.
    /// Uses zero-allocation deserialization via DTsr001A.
    /// </summary>
    private async Task<OptionChainSnapshot?> LoadBinaryCacheAsync(string path, CancellationToken cancellationToken)
    {
        using PooledBuffer buffer = PLBF001A.RentBuffer(PLBF001A.LargeBufferSize);
        using FileStream stream = File.OpenRead(path);
        
        int bytesRead = await stream.ReadAsync(buffer.Memory, cancellationToken);
        if (bytesRead == 0)
        {
            return null;
        }
        
        return DTsr001A.DecodeOptionChainSnapshot(buffer.Array.AsSpan(0, bytesRead));
    }

    /// <summary>
    /// Saves option chain snapshot to binary cache file.
    /// </summary>
    public async Task SaveOptionChainCacheAsync(
        string symbol,
        OptionChainSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_sessionDataPath))
        {
            return;
        }
        
        string optionsDir = System.IO.Path.Combine(_sessionDataPath, "options");
        Directory.CreateDirectory(optionsDir);
        
        string binaryCachePath = System.IO.Path.Combine(optionsDir, $"{symbol.ToLowerInvariant()}.sbe");
        
        using PooledBuffer buffer = PLBF001A.RentBuffer(PLBF001A.LargeBufferSize);
        int bytesWritten = DTsr001A.EncodeOptionChainSnapshot(snapshot, buffer.Span);
        
        await File.WriteAllBytesAsync(binaryCachePath, buffer.Array.AsSpan(0, bytesWritten).ToArray(), cancellationToken);
        
        _logger.LogDebug("Saved {Count} options to binary cache for {Symbol}", snapshot.Contracts.Count, symbol);
    }

    /// <summary>
    /// Gets historical bars for Yang-Zhang RV calculation (minimum 30 days).
    /// Uses cached data first, falls back to live API if needed.
    /// </summary>
    private async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsForRvCalculationAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        // Use evaluation date (from LEAN simulation or live) instead of real-world time
        DateTime endDate = evaluationDate.Date;
        DateTime startDate = endDate.AddDays(-45); // 45 days to ensure 30 trading days

        // Try cache first (backtest mode with pre-downloaded data)
        IReadOnlyList<PriceBar>? cachedBars = GetHistoricalBarsFromCache(symbol, startDate, endDate);
        if (cachedBars != null && cachedBars.Count >= 30)
        {
            _logger.LogDebug("Using {Count} cached historical bars for {Symbol}", cachedBars.Count, symbol);
            return cachedBars;
        }

        // Fall back to live API
        IReadOnlyList<PriceBar> bars = await RequireMarketDataProvider().GetHistoricalBarsAsync(
            symbol,
            startDate,
            endDate,
            cancellationToken);

        if (bars.Count < 30)
        {
            _logger.LogWarning(
                "Insufficient historical bars for {Symbol}: {Count} (need 30+)",
                symbol, bars.Count);
        }

        return bars;
    }

    /// <summary>
    /// Gets earnings data (next event + historical for Leung-Santoli).
    /// </summary>
    /// <param name="symbol">The symbol to fetch earnings for.</param>
    /// <param name="evaluationDate">The evaluation date (simulation time for backtests).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<(EarningsEvent? next, IReadOnlyList<EarningsEvent> historical)> GetEarningsDataAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        // Try cache first (backtest mode)
        (EarningsEvent? cachedNext, List<EarningsEvent> cachedHistorical) = GetEarningsFromCache(symbol, evaluationDate);
        if (cachedNext != null || cachedHistorical.Count > 0)
        {
            _logger.LogDebug("Using cached earnings for {Symbol}: Next={NextDate}, Historical={Count}",
                symbol, cachedNext?.Date.ToString("yyyy-MM-dd") ?? "None", cachedHistorical.Count);
            return (cachedNext, cachedHistorical);
        }

        if (!_allowEarningsFallback)
        {
            _logger.LogDebug("Earnings fallback disabled for {Symbol} @ {Date}", symbol, evaluationDate);
            return (null, Array.Empty<EarningsEvent>());
        }

        // Fall back to live API
        Task<IReadOnlyList<EarningsEvent>> upcomingTask = _earningsProvider.GetUpcomingEarningsAsync(
            symbol,
            daysAhead: 90,
            cancellationToken);

        Task<IReadOnlyList<EarningsEvent>> historicalTask = _earningsProvider.GetHistoricalEarningsAsync(
            symbol,
            lookbackDays: 730, // 2 years for Leung-Santoli calibration
            cancellationToken);

        await Task.WhenAll(upcomingTask, historicalTask);

        IReadOnlyList<EarningsEvent> upcoming = await upcomingTask;
        IReadOnlyList<EarningsEvent> historical = await historicalTask;

        // Find next earnings (closest upcoming)
        EarningsEvent? nextEarnings = null;
        foreach (EarningsEvent earning in upcoming)
        {
            if (nextEarnings == null || earning.Date < nextEarnings.Date)
            {
                nextEarnings = earning;
            }
        }

        // For backtesting: If upcoming is empty (SEC EDGAR only provides historical),
        // search historical data for dates AFTER the evaluation date
        if (nextEarnings == null && historical.Count > 0)
        {
            foreach (EarningsEvent earning in historical)
            {
                if (earning.Date <= evaluationDate.Date)
                {
                    continue;
                }

                if (nextEarnings == null || earning.Date < nextEarnings.Date)
                {
                    nextEarnings = earning;
                }
            }
                
            if (nextEarnings != null)
            {
                _logger.LogDebug(
                    "Found 'future' earnings for {Symbol} from historical data: {Date}",
                    symbol, nextEarnings.Date.ToString("yyyy-MM-dd"));
            }
        }

        // Filter historical to only include dates BEFORE evaluation date
        List<EarningsEvent> historicalBeforeEvaluation = new List<EarningsEvent>();
        foreach (EarningsEvent earning in historical)
        {
            if (earning.Date <= evaluationDate.Date)
            {
                historicalBeforeEvaluation.Add(earning);
            }
        }

        _logger.LogDebug(
            "Earnings data for {Symbol}: Next={NextDate}, Historical={HistoricalCount}",
            symbol,
            nextEarnings?.Date.ToString("yyyy-MM-dd") ?? "None",
            historicalBeforeEvaluation.Count);

        return (nextEarnings, historicalBeforeEvaluation);
    }

    /// <summary>
    /// Runs data quality validation pipeline.
    /// </summary>
    private IReadOnlyList<DataQualityResult> RunDataQualityValidation(MarketDataSnapshot snapshot)
    {
        _logger.LogDebug("Running data quality validation for {Symbol}", snapshot.Symbol);

        List<DataQualityResult> results = new List<DataQualityResult>();

        foreach (DTqc002A validator in _validators)
        {
            try
            {
                DataQualityResult result = validator.Validate(snapshot);
                results.Add(result);

                _logger.LogDebug(
                    "Validator {ValidatorId}: {Status} - {Message}",
                    result.ValidatorId,
                    result.Status,
                    result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validator {ValidatorId} threw exception", validator.ComponentId);
                
                results.Add(new DataQualityResult
                {
                    ValidatorId = validator.ComponentId,
                    Status = ValidationStatus.Failed,
                    Message = $"Validator exception: {ex.Message}",
                    DataElement = "N/A"
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Gets all symbols with upcoming earnings in date range.
    /// Useful for universe selection in Lean algorithm.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetSymbolsWithUpcomingEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Getting symbols with earnings from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        IReadOnlyList<string> symbols = await _earningsProvider.GetSymbolsWithEarningsAsync(
            startDate,
            endDate,
            cancellationToken);

        _logger.LogInformation("Found {Count} symbols with upcoming earnings", symbols.Count);

        return symbols;
    }

    /// <summary>
    /// Validates if a symbol meets basic criteria for strategy evaluation.
    /// Used for pre-filtering universe before expensive data fetches.
    /// </summary>
    public async Task<bool> MeetsBasicCriteriaAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check 1: Has upcoming earnings within 90 days
            IReadOnlyList<EarningsEvent> upcomingEarnings = await _earningsProvider.GetUpcomingEarningsAsync(
                symbol,
                daysAhead: 90,
                cancellationToken);

            if (upcomingEarnings.Count == 0)
            {
                _logger.LogDebug("{Symbol}: No upcoming earnings", symbol);
                return false;
            }

            // Check 2: Sufficient average volume (Atilgan threshold: 1.5M)
            decimal avgVolume = await RequireMarketDataProvider().GetAverageVolume30DayAsync(
                symbol,
                null, // Use current date for live pre-filtering
                cancellationToken);

            if (avgVolume < 1_500_000m)
            {
                _logger.LogDebug("{Symbol}: Insufficient volume ({Volume:N0})", symbol, avgVolume);
                return false;
            }

            _logger.LogDebug("{Symbol}: Meets basic criteria", symbol);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking basic criteria for {Symbol}", symbol);
            return false;
        }
    }

    /// <summary>
    /// Gets risk-free rate from cached interest rate data.
    /// Reads from pre-downloaded interest-rate.csv file.
    /// </summary>
    /// <param name="evaluationDate">The evaluation date to get rate for.</param>
    /// <returns>Risk-free rate, or null if no cache available.</returns>
    private decimal? GetRiskFreeRateFromCache(DateTime evaluationDate)
    {
        if (string.IsNullOrEmpty(_sessionDataPath))
        {
            return null;
        }

        string ratePath = Path.Combine(_sessionDataPath, "alternative", "interest-rate", "usa", "interest-rate.csv");
        if (!File.Exists(ratePath))
        {
            _logger.LogDebug("Interest rate cache not found at {Path}", ratePath);
            return null;
        }

        try
        {
            string[] lines = File.ReadAllLines(ratePath);
            if (lines.Length == 0)
            {
                return null;
            }

            // Parse CSV: format is "yyyyMMdd,rate" where rate is decimal (e.g., 0.05 for 5%)
            DateTime targetDate = evaluationDate.Date;
            decimal? closestRate = null;
            DateTime closestDate = DateTime.MinValue;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(',');
                if (parts.Length < 2)
                {
                    continue;
                }

                if (!DateTime.TryParseExact(parts[0], "yyyyMMdd", null, 
                    System.Globalization.DateTimeStyles.None, out DateTime rateDate))
                {
                    continue;
                }

                if (!decimal.TryParse(parts[1], out decimal rate))
                {
                    continue;
                }

                // Find the closest date <= evaluation date
                if (rateDate <= targetDate && rateDate > closestDate)
                {
                    closestDate = rateDate;
                    closestRate = rate;
                }
            }

            if (closestRate.HasValue)
            {
                _logger.LogDebug("Loaded interest rate {Rate:P4} from cache (date: {Date:yyyy-MM-dd})", 
                    closestRate.Value, closestDate);
            }

            return closestRate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading interest rate cache");
            return null;
        }
    }

    /// <summary>
    /// Gets historical bars from cached price data.
    /// Reads from pre-downloaded equity data (ZIP or CSV files).
    /// </summary>
    /// <param name="symbol">The symbol to get bars for.</param>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <returns>List of price bars, or null if no cache available.</returns>
    private IReadOnlyList<PriceBar>? GetHistoricalBarsFromCache(string symbol, DateTime startDate, DateTime endDate)
    {
        if (string.IsNullOrEmpty(_sessionDataPath))
        {
            return null;
        }

        string symbolLower = symbol.ToLowerInvariant();
        string dailyDir = Path.Combine(_sessionDataPath, "equity", "usa", "daily");
        
        // LEAN stores equity data as ZIP files (e.g., aapl.zip containing aapl.csv)
        string zipPath = Path.Combine(dailyDir, $"{symbolLower}.zip");
        string csvPath = Path.Combine(dailyDir, $"{symbolLower}.csv");
        
        string[]? lines = null;
        
        // Try ZIP first (LEAN format)
        if (File.Exists(zipPath))
        {
            try
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                var entry = archive.GetEntry($"{symbolLower}.csv");
                if (entry != null)
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    lines = reader.ReadToEnd().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    _logger.LogDebug("Loaded {Count} lines from ZIP cache for {Symbol}", lines.Length, symbol);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading ZIP cache for {Symbol}", symbol);
            }
        }
        
        // Fallback to direct CSV
        if (lines == null && File.Exists(csvPath))
        {
            lines = File.ReadAllLines(csvPath);
        }
        
        if (lines == null || lines.Length == 0)
        {
            _logger.LogDebug("Price cache not found for {Symbol} at {Path}", symbol, dailyDir);
            return null;
        }

        try
        {
            List<PriceBar> bars = new List<PriceBar>();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // LEAN CSV format: Date,Open,High,Low,Close,Volume
                // Date format: yyyyMMdd HH:mm or yyyyMMdd
                string[] parts = line.Split(',');
                if (parts.Length < 6)
                {
                    continue;
                }

                // Parse date - try both formats
                string datePart = parts[0].Split(' ')[0]; // Get just the date part
                if (!DateTime.TryParseExact(datePart, "yyyyMMdd", null, 
                    System.Globalization.DateTimeStyles.None, out DateTime barDate))
                {
                    continue;
                }

                // Filter by end date only - return all available bars up to endDate
                // Don't filter by startDate because cache may not extend that far back
                // The caller can handle having more history than requested
                if (barDate > endDate)
                {
                    continue;
                }

                // Parse OHLCV - LEAN uses scaled prices (divide by 10000)
                if (!decimal.TryParse(parts[1], out decimal openRaw) ||
                    !decimal.TryParse(parts[2], out decimal highRaw) ||
                    !decimal.TryParse(parts[3], out decimal lowRaw) ||
                    !decimal.TryParse(parts[4], out decimal closeRaw) ||
                    !long.TryParse(parts[5], out long volume))
                {
                    continue;
                }

                // LEAN stores prices scaled by 10000
                decimal scaleFactor = 10000m;
                bars.Add(new PriceBar
                {
                    Symbol = symbol,
                    Timestamp = barDate,
                    Open = openRaw / scaleFactor,
                    High = highRaw / scaleFactor,
                    Low = lowRaw / scaleFactor,
                    Close = closeRaw / scaleFactor,
                    Volume = volume
                });
            }

            if (bars.Count > 0)
            {
                // Sort by date ascending
                bars.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                _logger.LogDebug("Loaded {Count} bars from cache for {Symbol}", bars.Count, symbol);
            }

            return bars.Count > 0 ? bars : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading price cache for {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Computes 30-day average volume from cached price bars.
    /// </summary>
    /// <param name="symbol">The symbol to compute volume for.</param>
    /// <param name="evaluationDate">The evaluation date.</param>
    /// <returns>Average 30-day volume, or null if insufficient data.</returns>
    private decimal? GetAverageVolumeFromCache(string symbol, DateTime evaluationDate)
    {
        DateTime endDate = evaluationDate.Date;
        DateTime startDate = endDate.AddDays(-45); // 45 days to ensure 30 trading days

        IReadOnlyList<PriceBar>? bars = GetHistoricalBarsFromCache(symbol, startDate, endDate);
        if (bars == null || bars.Count < 20) // Need at least 20 days for a reasonable average
        {
            return null;
        }

        // Take last 30 bars (or all if less)
        int takeCount = Math.Min(30, bars.Count);
        long totalVolume = 0;
        for (int i = bars.Count - takeCount; i < bars.Count; i++)
        {
            totalVolume += bars[i].Volume;
        }

        decimal avgVolume = (decimal)totalVolume / takeCount;
        _logger.LogDebug("Computed 30d avg volume {Volume:N0} from {Count} cached bars for {Symbol}", 
            avgVolume, takeCount, symbol);
        return avgVolume;
    }

    /// <summary>
    /// Computes 30-day average volume from provided historical bars.
    /// Used to avoid separate API call for volume data.
    /// </summary>
    /// <param name="historicalBars">Historical price bars with volume data.</param>
    /// <param name="symbol">Symbol for logging.</param>
    /// <param name="evaluationDate">Evaluation date for logging.</param>
    /// <returns>Average 30-day volume.</returns>
    private decimal ComputeAverageVolumeFromBars(
        IReadOnlyList<PriceBar> historicalBars, 
        string symbol, 
        DateTime evaluationDate)
    {
        if (historicalBars.Count == 0)
        {
            _logger.LogWarning("No historical bars available to compute average volume for {Symbol}", symbol);
            return 0m;
        }

        // Take last 30 bars (or all available if less)
        int takeCount = Math.Min(30, historicalBars.Count);
        long totalVolume = 0;
        
        for (int i = historicalBars.Count - takeCount; i < historicalBars.Count; i++)
        {
            totalVolume += historicalBars[i].Volume;
        }

        decimal avgVolume = (decimal)totalVolume / takeCount;
        _logger.LogDebug("Computed 30d avg volume {Volume:N0} from {Count} bars for {Symbol} @ {Date:yyyy-MM-dd}", 
            avgVolume, takeCount, symbol, evaluationDate);
        
        return avgVolume;
    }

    /// <summary>
    /// Gets earnings data from cached files.
    /// Cached earnings are stored by date in earnings/nasdaq/YYYY-MM-DD.json
    /// </summary>
    private (EarningsEvent? next, List<EarningsEvent> historical) GetEarningsFromCache(
        string symbol,
        DateTime evaluationDate)
    {
        if (string.IsNullOrEmpty(_sessionDataPath))
        {
            return (null, new List<EarningsEvent>());
        }

        string earningsDir = Path.Combine(_sessionDataPath, "earnings", "nasdaq");
        if (!Directory.Exists(earningsDir))
        {
            _logger.LogDebug("Earnings cache directory not found: {Path}", earningsDir);
            return (null, new List<EarningsEvent>());
        }

        try
        {
            EarningsEvent? nextEarnings = null;
            List<EarningsEvent> historicalEarnings = new List<EarningsEvent>();
            string symbolUpper = symbol.ToUpperInvariant();

            // Scan earnings files for this symbol
            // Search from evaluation date back 2 years for historical, and forward 90 days for next
            DateTime searchStart = evaluationDate.AddYears(-2);
            DateTime searchEnd = evaluationDate.AddDays(90);

            string[] files = Directory.GetFiles(earningsDir, "????-??-??.json");
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                if (!DateTime.TryParseExact(filename, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                {
                    continue;
                }

                if (fileDate < searchStart || fileDate > searchEnd)
                {
                    continue;
                }

                // Read and parse the file
                string json = File.ReadAllText(file);
                using JsonDocument doc = JsonDocument.Parse(json);
                
                if (!doc.RootElement.TryGetProperty("earnings", out JsonElement earningsArray))
                {
                    continue;
                }

                foreach (JsonElement earningElem in earningsArray.EnumerateArray())
                {
                    if (!earningElem.TryGetProperty("symbol", out JsonElement symbolElem))
                    {
                        continue;
                    }
                    
                    if (!string.Equals(symbolElem.GetString(), symbolUpper, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Parse the earnings event
                    EarningsEvent earning = new EarningsEvent
                    {
                        Symbol = symbolUpper,
                        Date = fileDate,
                        FiscalQuarter = earningElem.TryGetProperty("fiscalQuarter", out JsonElement fq) ? fq.GetString() ?? "" : "",
                        EpsEstimate = earningElem.TryGetProperty("epsEstimate", out JsonElement est) && est.ValueKind == JsonValueKind.Number ? est.GetDecimal() : null,
                        EpsActual = earningElem.TryGetProperty("epsActual", out JsonElement act) && act.ValueKind == JsonValueKind.Number ? act.GetDecimal() : null,
                        Source = "Cached",
                        FetchedAt = DateTime.UtcNow
                    };

                    if (fileDate > evaluationDate)
                    {
                        // Future earnings
                        if (nextEarnings == null || earning.Date < nextEarnings.Date)
                        {
                            nextEarnings = earning;
                        }
                    }
                    else
                    {
                        // Historical earnings
                        historicalEarnings.Add(earning);
                    }
                }
            }

            // Sort historical by date descending (most recent first)
            historicalEarnings.Sort((a, b) => b.Date.CompareTo(a.Date));

            if (nextEarnings != null || historicalEarnings.Count > 0)
            {
                _logger.LogDebug("Found cached earnings for {Symbol}: Next={Next}, Historical={Count}",
                    symbol, nextEarnings?.Date.ToString("yyyy-MM-dd") ?? "None", historicalEarnings.Count);
            }

            return (nextEarnings, historicalEarnings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading earnings cache for {Symbol}", symbol);
            return (null, new List<EarningsEvent>());
        }
    }
}
