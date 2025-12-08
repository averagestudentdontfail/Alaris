using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Data.Model;
using Alaris.Data.Provider;
using Alaris.Data.Quality;

namespace Alaris.Data.Bridge;

/// <summary>
/// Bridges Alaris data providers to Lean engine's data infrastructure.
/// Component ID: DTbr001A
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Aggregates data from multiple providers (Polygon, FMP, IBKR)
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
    /// <param name="earningsProvider">Earnings calendar provider (FMP).</param>
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
        // Use provided date or fall back to UTC now for live trading
        var effectiveDate = evaluationDate ?? DateTime.UtcNow;
        _logger.LogInformation("Building market data snapshot for {Symbol} as of {Date:yyyy-MM-dd}", symbol, effectiveDate);

        try
        {
            // Step 1: Fetch all data concurrently
            var spotPriceTask = _marketDataProvider.GetSpotPriceAsync(symbol, cancellationToken);
            var historicalBarsTask = GetHistoricalBarsForRvCalculationAsync(symbol, effectiveDate, cancellationToken);
            var optionChainTask = GetOptionChainWithCacheFallbackAsync(symbol, effectiveDate, cancellationToken);
            var earningsTask = GetEarningsDataAsync(symbol, effectiveDate, cancellationToken);
            var riskFreeRateTask = _riskFreeRateProvider.GetCurrentRateAsync(cancellationToken);
            var avgVolumeTask = _marketDataProvider.GetAverageVolume30DayAsync(symbol, effectiveDate, cancellationToken);

            await Task.WhenAll(
                spotPriceTask,
                historicalBarsTask,
                optionChainTask,
                earningsTask,
                riskFreeRateTask,
                avgVolumeTask);

            var spotPrice = await spotPriceTask;
            var historicalBars = await historicalBarsTask;
            var optionChain = await optionChainTask;
            var (nextEarnings, historicalEarnings) = await earningsTask;
            var riskFreeRate = await riskFreeRateTask;
            var avgVolume = await avgVolumeTask;

            // Step 2: Construct snapshot
            var snapshot = new MarketDataSnapshot
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
            var validationResults = RunDataQualityValidation(snapshot);

            // Step 4: Check for critical failures
            var criticalFailures = validationResults
                .Where(r => r.Status == ValidationStatus.Failed)
                .ToList();

            if (criticalFailures.Count > 0)
            {
                var errors = string.Join("; ", criticalFailures.Select(r => r.Message));
                _logger.LogError(
                    "Data quality validation failed for {Symbol}: {Errors}",
                    symbol, errors);

                throw new InvalidOperationException(
                    $"Data quality validation failed: {errors}");
            }

            // Step 5: Log warnings
            var warnings = validationResults
                .Where(r => r.Status == ValidationStatus.PassedWithWarnings)
                .SelectMany(r => r.Warnings)
                .ToList();

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
    /// Gets option chain with cache fallback for backtest mode.
    /// Loads from session cache if available, otherwise falls back to live API.
    /// </summary>
    private async Task<OptionChainSnapshot> GetOptionChainWithCacheFallbackAsync(
        string symbol,
        DateTime evaluationDate,
        CancellationToken cancellationToken)
    {
        // Try to load from session cache first (backtest mode)
        if (!string.IsNullOrEmpty(_sessionDataPath))
        {
            var cacheFilePath = System.IO.Path.Combine(_sessionDataPath, "options", $"{symbol.ToLowerInvariant()}.json");
            
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
                    var cached = JsonSerializer.Deserialize<OptionChainSnapshot>(json, JsonOptions);
                    
                    if (cached != null && cached.Contracts.Count > 0)
                    {
                        _logger.LogDebug("Loaded {Count} options contracts from cache for {Symbol}", 
                            cached.Contracts.Count, symbol);
                        
                        // Update timestamp to evaluation date
                        return new OptionChainSnapshot
                        {
                            Symbol = cached.Symbol,
                            SpotPrice = cached.SpotPrice,
                            Timestamp = evaluationDate,
                            Contracts = cached.Contracts
                        };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize options cache for {Symbol}", symbol);
                }
            }
            else
            {
                _logger.LogDebug("No options cache file found for {Symbol} at {Path}", symbol, cacheFilePath);
            }
        }

        // Fall back to live API (for live trading or if cache not available)
        _logger.LogDebug("Fetching live option chain for {Symbol}", symbol);
        return await _marketDataProvider.GetOptionChainAsync(symbol, cancellationToken);
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
        var endDate = evaluationDate.Date;
        var startDate = endDate.AddDays(-45); // 45 days to ensure 30 trading days

        var bars = await _marketDataProvider.GetHistoricalBarsAsync(
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
        var upcomingTask = _earningsProvider.GetUpcomingEarningsAsync(
            symbol,
            daysAhead: 90,
            cancellationToken);

        var historicalTask = _earningsProvider.GetHistoricalEarningsAsync(
            symbol,
            lookbackDays: 730, // 2 years for Leung-Santoli calibration
            cancellationToken);

        await Task.WhenAll(upcomingTask, historicalTask);

        var upcoming = await upcomingTask;
        var historical = await historicalTask;

        // Find next earnings (closest upcoming)
        EarningsEvent? nextEarnings = upcoming
            .OrderBy(e => e.Date)
            .FirstOrDefault();

        // For backtesting: If upcoming is empty (SEC EDGAR only provides historical),
        // search historical data for dates AFTER the evaluation date
        if (nextEarnings == null && historical.Count > 0)
        {
            nextEarnings = historical
                .Where(e => e.Date > evaluationDate.Date)
                .OrderBy(e => e.Date)
                .FirstOrDefault();
                
            if (nextEarnings != null)
            {
                _logger.LogDebug(
                    "Found 'future' earnings for {Symbol} from historical data: {Date}",
                    symbol, nextEarnings.Date.ToString("yyyy-MM-dd"));
            }
        }

        // Filter historical to only include dates BEFORE evaluation date
        var historicalBeforeEvaluation = historical
            .Where(e => e.Date <= evaluationDate.Date)
            .ToList();

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

        var results = new List<DataQualityResult>();

        foreach (var validator in _validators)
        {
            try
            {
                var result = validator.Validate(snapshot);
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

        var symbols = await _earningsProvider.GetSymbolsWithEarningsAsync(
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
            var upcomingEarnings = await _earningsProvider.GetUpcomingEarningsAsync(
                symbol,
                daysAhead: 90,
                cancellationToken);

            if (upcomingEarnings.Count == 0)
            {
                _logger.LogDebug("{Symbol}: No upcoming earnings", symbol);
                return false;
            }

            // Check 2: Sufficient average volume (Atilgan threshold: 1.5M)
            var avgVolume = await _marketDataProvider.GetAverageVolume30DayAsync(
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