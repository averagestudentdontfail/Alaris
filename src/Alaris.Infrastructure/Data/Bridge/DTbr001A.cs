using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly DTpr003A _marketDataProvider;
    private readonly DTpr004A _earningsProvider;
    private readonly DTpr005A _riskFreeRateProvider;
    private readonly IReadOnlyList<DTqc002A> _validators;
    private readonly ILogger<AlarisDataBridge> _logger;
    
    // Session data path for loading cached data (options, etc.)
    private string? _sessionDataPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="AlarisDataBridge"/> class.
    /// </summary>
    /// <param name="marketDataProvider">Market data provider (Polygon).</param>
    /// <param name="earningsProvider">Earnings calendar provider.</param>
    /// <param name="riskFreeRateProvider">Risk-free rate provider.</param>
    /// <param name="validators">Data quality validators.</param>
    /// <param name="logger">Logger instance.</param>
    public AlarisDataBridge(
        DTpr003A marketDataProvider,
        DTpr004A earningsProvider,
        DTpr005A riskFreeRateProvider,
        IReadOnlyList<DTqc002A> validators,
        ILogger<AlarisDataBridge> logger)
    {
        _marketDataProvider = marketDataProvider ?? throw new ArgumentNullException(nameof(marketDataProvider));
        _earningsProvider = earningsProvider ?? throw new ArgumentNullException(nameof(earningsProvider));
        _riskFreeRateProvider = riskFreeRateProvider ?? throw new ArgumentNullException(nameof(riskFreeRateProvider));
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            Task<long> avgVolumeTask = _marketDataProvider.GetAverageVolume30DayAsync(symbol, effectiveDate, cancellationToken);

            await Task.WhenAll(
                historicalBarsTask,
                optionChainTask,
                earningsTask,
                avgVolumeTask);

            IReadOnlyList<PriceBar> historicalBars = await historicalBarsTask;
            OptionChainSnapshot optionChain = await optionChainTask;
            (EarningsEvent? nextEarnings, IReadOnlyList<EarningsEvent> historicalEarnings) = await earningsTask;
            long avgVolume = await avgVolumeTask;
            
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
                spotPrice = await _marketDataProvider.GetSpotPriceAsync(symbol, cancellationToken);
            }
            
            // For backtesting, use a reasonable historical risk-free rate
            // The Treasury API returns 0% for future dates, so we use a sensible default
            decimal riskFreeRate;
            if (evaluationDate.HasValue && evaluationDate.Value > DateTime.UtcNow.Date)
            {
                // Future date in backtest - use typical historical rate
                riskFreeRate = 0.05m; // 5% typical for 2023-2024
                _logger.LogDebug("Using default risk-free rate {Rate}% for backtest date {Date}", 
                    riskFreeRate * 100, effectiveDate);
            }
            else
            {
                riskFreeRate = await _riskFreeRateProvider.GetCurrentRateAsync(cancellationToken);
            }

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
                DividendYield = 0.005m, // TODO: Fetch actual dividend yield
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

        // Fetch from provider (Live or Historical On-Demand)
        _logger.LogDebug("Fetching option chain for {Symbol} as of {Date}", symbol, evaluationDate);
        return await _marketDataProvider.GetOptionChainAsync(symbol, evaluationDate, cancellationToken);
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
            (DateTime date, string path, bool isBinary) selected = default;

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
    /// </summary>
    private async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsForRvCalculationAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        // Use evaluation date (from LEAN simulation or live) instead of real-world time
        DateTime endDate = evaluationDate.Date;
        DateTime startDate = endDate.AddDays(-45); // 45 days to ensure 30 trading days

        IReadOnlyList<PriceBar> bars = await _marketDataProvider.GetHistoricalBarsAsync(
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
        // Fetch upcoming and historical concurrently
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
            long avgVolume = await _marketDataProvider.GetAverageVolume30DayAsync(
                symbol,
                null, // Use current date for live pre-filtering
                cancellationToken);

            if (avgVolume < 1_500_000)
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
}
