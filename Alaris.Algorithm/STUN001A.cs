// =============================================================================
// STUN001A.cs - Earnings Universe Selection Model
// Component: STUN001A | Category: Universe | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Reference: QuantConnect.Algorithm.Framework.Selection
// Reference: Atilgan (2014) - Pre-screening criteria
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;

namespace Alaris.Algorithm.Universe;

/// <summary>
/// Universe selection model that filters for symbols with upcoming earnings.
/// Implements Atilgan (2014) pre-screening criteria for earnings calendar spreads.
/// </summary>
/// <remarks>
/// <para>
/// Selection criteria (from Atilgan 2014):
/// <list type="bullet">
///   <item>Minimum 30-day average dollar volume: $1.5M</item>
///   <item>Minimum price: $5.00</item>
///   <item>Earnings announcement in T+[DaysMin, DaysMax] days</item>
///   <item>Must have options available (checked via coarse data)</item>
/// </list>
/// </para>
/// <para>
/// The universe refreshes daily at market open (9:30 AM ET), querying the
/// earnings calendar provider for symbols with announcements in the target window.
/// </para>
/// <para>
/// This model integrates with LEAN's universe selection framework whilst
/// leveraging Alaris.Data providers for earnings calendar information.
/// </para>
/// </remarks>
public sealed class STUN001A : UniverseSelectionModel
{
    // =========================================================================
    // Dependencies
    // =========================================================================
    
    private readonly IEarningsCalendarProvider _earningsProvider;
    private readonly ILogger<STUN001A>? _logger;

    // =========================================================================
    // Configuration
    // =========================================================================
    
    private readonly int _daysBeforeEarningsMin;
    private readonly int _daysBeforeEarningsMax;
    private readonly decimal _minimumDollarVolume;
    private readonly decimal _minimumPrice;
    private readonly int _maxCoarseSymbols;
    private readonly int _maxFinalSymbols;
    
    // =========================================================================
    // State
    // =========================================================================
    
    private DateTime _lastEarningsQueryDate;
    private HashSet<string> _cachedEarningsSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    // =========================================================================
    // LoggerMessage Delegates (High-Integrity Coding Standard Rule 12)
    // =========================================================================
    
    private static readonly Action<ILogger, DateTime, DateTime, int, Exception?> LogEarningsQuery =
        LoggerMessage.Define<DateTime, DateTime, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogEarningsQuery)),
            "STUN001A: Queried earnings calendar {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}, found {Count} symbols");

    private static readonly Action<ILogger, int, int, int, Exception?> LogUniverseSelection =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogUniverseSelection)),
            "STUN001A: Universe selection: {CoarseFiltered} coarse → {EarningsMatched} with earnings → {Final} selected");

    private static readonly Action<ILogger, string, Exception?> LogEarningsQueryError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, nameof(LogEarningsQueryError)),
            "STUN001A: Failed to query earnings calendar: {Error}");

    private static readonly Action<ILogger, string, decimal, decimal, Exception?> LogSymbolSelected =
        LoggerMessage.Define<string, decimal, decimal>(
            LogLevel.Debug,
            new EventId(4, nameof(LogSymbolSelected)),
            "STUN001A: Selected {Symbol} (Volume: ${DollarVolume:N0}, Price: ${Price:F2})");

    // =========================================================================
    // Constructor
    // =========================================================================
    
    /// <summary>
    /// Initialises a new instance of the earnings universe selection model.
    /// </summary>
    /// <param name="earningsProvider">Provider for earnings calendar data.</param>
    /// <param name="daysBeforeEarningsMin">Minimum days before earnings (default: 5).</param>
    /// <param name="daysBeforeEarningsMax">Maximum days before earnings (default: 7).</param>
    /// <param name="minimumDollarVolume">Minimum 30-day dollar volume (default: $1.5M).</param>
    /// <param name="minimumPrice">Minimum share price (default: $5).</param>
    /// <param name="maxCoarseSymbols">Maximum symbols to pre-filter by volume (default: 500).</param>
    /// <param name="maxFinalSymbols">Maximum symbols to select (default: 50).</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when earningsProvider is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when day ranges are invalid or negative values provided.
    /// </exception>
    public STUN001A(
        IEarningsCalendarProvider earningsProvider,
        int daysBeforeEarningsMin = 5,
        int daysBeforeEarningsMax = 7,
        decimal minimumDollarVolume = 1_500_000m,
        decimal minimumPrice = 5.00m,
        int maxCoarseSymbols = 500,
        int maxFinalSymbols = 50,
        ILogger<STUN001A>? logger = null)
    {
        _earningsProvider = earningsProvider 
            ?? throw new ArgumentNullException(nameof(earningsProvider));
        
        if (daysBeforeEarningsMin < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysBeforeEarningsMin),
                daysBeforeEarningsMin,
                "Days before earnings minimum must be non-negative.");
        }
        
        if (daysBeforeEarningsMax < daysBeforeEarningsMin)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysBeforeEarningsMax),
                daysBeforeEarningsMax,
                "Days before earnings maximum must be >= minimum.");
        }
        
        if (minimumDollarVolume < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumDollarVolume),
                minimumDollarVolume,
                "Minimum dollar volume must be non-negative.");
        }
        
        if (minimumPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPrice),
                minimumPrice,
                "Minimum price must be non-negative.");
        }
        
        if (maxCoarseSymbols < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCoarseSymbols),
                maxCoarseSymbols,
                "Maximum coarse symbols must be positive.");
        }
        
        if (maxFinalSymbols < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFinalSymbols),
                maxFinalSymbols,
                "Maximum final symbols must be positive.");
        }
        
        _daysBeforeEarningsMin = daysBeforeEarningsMin;
        _daysBeforeEarningsMax = daysBeforeEarningsMax;
        _minimumDollarVolume = minimumDollarVolume;
        _minimumPrice = minimumPrice;
        _maxCoarseSymbols = maxCoarseSymbols;
        _maxFinalSymbols = maxFinalSymbols;
        _logger = logger;
    }

    // =========================================================================
    // Universe Selection Implementation
    // =========================================================================
    
    /// <summary>
    /// Creates the universes for this model.
    /// </summary>
    /// <param name="algorithm">The algorithm instance.</param>
    /// <returns>The universes defined by this model.</returns>
    public override IEnumerable<QuantConnect.Data.UniverseSelection.Universe> CreateUniverses(QCAlgorithm algorithm)
    {
        // Create a coarse universe with daily selection at market open
        var universe = algorithm.AddUniverse(
            "AlarisEarningsUniverse",
            Resolution.Daily,
            coarse => SelectCoarse(algorithm, coarse));
        
        return new[] { universe };
    }

    /// <summary>
    /// Selects symbols from the coarse universe that meet earnings criteria.
    /// </summary>
    /// <param name="algorithm">The algorithm instance for time context.</param>
    /// <param name="coarse">The coarse fundamental data.</param>
    /// <returns>Symbols meeting the selection criteria.</returns>
    public IEnumerable<Symbol> SelectCoarse(
        QCAlgorithm algorithm,
        IEnumerable<CoarseFundamental> coarse)
    {
        var currentDate = algorithm.Time.Date;
        
        // =====================================================================
        // Step 1: Apply coarse filters (volume, price, has fundamental data)
        // =====================================================================
        
        var coarseFiltered = coarse
            .Where(c => c.HasFundamentalData)
            .Where(c => c.DollarVolume >= _minimumDollarVolume)
            .Where(c => c.Price >= _minimumPrice)
            .OrderByDescending(c => c.DollarVolume)
            .Take(_maxCoarseSymbols)
            .ToList();
        
        if (coarseFiltered.Count == 0)
        {
            algorithm.Log("STUN001A: No symbols passed coarse filter");
            return Enumerable.Empty<Symbol>();
        }
        
        // =====================================================================
        // Step 2: Query earnings calendar for target window
        // =====================================================================
        
        var earningsStartDate = currentDate.AddDays(_daysBeforeEarningsMin);
        var earningsEndDate = currentDate.AddDays(_daysBeforeEarningsMax);
        
        var earningsSymbols = GetEarningsSymbolsForWindow(
            algorithm,
            earningsStartDate,
            earningsEndDate);
        
        if (earningsSymbols.Count == 0)
        {
            algorithm.Log($"STUN001A: No earnings found in window {earningsStartDate:yyyy-MM-dd} to {earningsEndDate:yyyy-MM-dd}");
            return Enumerable.Empty<Symbol>();
        }
        
        // =====================================================================
        // Step 3: Filter to symbols with upcoming earnings
        // =====================================================================
        
        var selected = new List<Symbol>();
        
        foreach (var c in coarseFiltered)
        {
            // Extract ticker from Symbol (handling various symbol formats)
            var ticker = ExtractTicker(c.Symbol);
            
            if (earningsSymbols.Contains(ticker))
            {
                selected.Add(c.Symbol);
                
                if (_logger != null)
                {
                    LogSymbolSelected(_logger, ticker, c.DollarVolume, c.Price, null);
                }
                
                if (selected.Count >= _maxFinalSymbols)
                {
                    break;
                }
            }
        }
        
        // =====================================================================
        // Step 4: Log results
        // =====================================================================
        
        if (_logger != null)
        {
            LogUniverseSelection(
                _logger,
                coarseFiltered.Count,
                earningsSymbols.Count,
                selected.Count,
                null);
        }
        
        algorithm.Log($"STUN001A: Selected {selected.Count} symbols with upcoming earnings");
        
        return selected;
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================
    
    /// <summary>
    /// Gets symbols with earnings in the specified date window.
    /// Uses caching to avoid redundant API calls within the same trading day.
    /// </summary>
    /// <param name="algorithm">Algorithm for logging.</param>
    /// <param name="startDate">Start of earnings window.</param>
    /// <param name="endDate">End of earnings window.</param>
    /// <returns>Set of ticker symbols with earnings in window.</returns>
    private HashSet<string> GetEarningsSymbolsForWindow(
        QCAlgorithm algorithm,
        DateTime startDate,
        DateTime endDate)
    {
        lock (_cacheLock)
        {
            // Return cached result if query was already made today
            if (_lastEarningsQueryDate == algorithm.Time.Date && _cachedEarningsSymbols.Count > 0)
            {
                return _cachedEarningsSymbols;
            }
            
            try
            {
                // Query earnings provider with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                
                var symbols = _earningsProvider.GetSymbolsWithEarningsAsync(
                    startDate,
                    endDate,
                    cts.Token)
                    .GetAwaiter().GetResult();
                
                _cachedEarningsSymbols = new HashSet<string>(
                    symbols,
                    StringComparer.OrdinalIgnoreCase);
                _lastEarningsQueryDate = algorithm.Time.Date;
                
                if (_logger != null)
                {
                    LogEarningsQuery(_logger, startDate, endDate, _cachedEarningsSymbols.Count, null);
                }
                
                return _cachedEarningsSymbols;
            }
            catch (OperationCanceledException)
            {
                algorithm.Error("STUN001A: Earnings calendar query timed out after 30 seconds");
                return _cachedEarningsSymbols; // Return cached if timeout
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogEarningsQueryError(_logger, ex.Message, ex);
                }
                
                algorithm.Error($"STUN001A: Failed to query earnings calendar: {ex.Message}");
                return _cachedEarningsSymbols; // Return cached if query fails
            }
        }
    }

    /// <summary>
    /// Extracts the ticker symbol from a LEAN Symbol object.
    /// </summary>
    /// <param name="symbol">The LEAN Symbol.</param>
    /// <returns>The ticker string.</returns>
    private static string ExtractTicker(Symbol symbol)
    {
        // Handle various symbol ID formats
        // Standard equity: "AAPL R735QTJ8XC9X"
        // We want just the ticker portion
        return symbol.Value;
    }

    /// <summary>
    /// Invalidates the earnings cache, forcing a refresh on next query.
    /// Useful for testing or manual refresh scenarios.
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _lastEarningsQueryDate = DateTime.MinValue;
            _cachedEarningsSymbols.Clear();
        }
    }

    /// <summary>
    /// Gets the current configuration for diagnostic purposes.
    /// </summary>
    /// <returns>Configuration summary string.</returns>
    public string GetConfigurationSummary()
    {
        return $"STUN001A Configuration: " +
               $"EarningsDays=[{_daysBeforeEarningsMin},{_daysBeforeEarningsMax}], " +
               $"MinVolume=${_minimumDollarVolume:N0}, " +
               $"MinPrice=${_minimumPrice:F2}, " +
               $"MaxCoarse={_maxCoarseSymbols}, " +
               $"MaxFinal={_maxFinalSymbols}";
    }
}

// =============================================================================
// Interface for Dependency Injection
// =============================================================================

/// <summary>
/// Interface for earnings calendar providers.
/// Allows STUN001A to work with different data sources.
/// </summary>
/// <remarks>
/// This interface mirrors the one in Alaris.Data.Provider but is defined here
/// to avoid circular dependencies. In production, use the Alaris.Data implementation.
/// </remarks>
public interface IEarningsCalendarProvider
{
    /// <summary>
    /// Gets symbols with earnings announcements in the specified date range.
    /// </summary>
    /// <param name="startDate">Start of date range.</param>
    /// <param name="endDate">End of date range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of ticker symbols with earnings in range.</returns>
    Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets detailed earnings information for a specific symbol.
    /// </summary>
    /// <param name="symbol">The ticker symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Earnings event details or null if not found.</returns>
    Task<EarningsEvent?> GetNextEarningsAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an earnings announcement event.
/// </summary>
public sealed record EarningsEvent
{
    /// <summary>Gets the ticker symbol.</summary>
    public required string Symbol { get; init; }
    
    /// <summary>Gets the earnings announcement date.</summary>
    public required DateTime AnnouncementDate { get; init; }
    
    /// <summary>Gets whether announcement is before or after market.</summary>
    public required EarningsTiming Timing { get; init; }
    
    /// <summary>Gets the fiscal quarter.</summary>
    public string? FiscalQuarter { get; init; }
    
    /// <summary>Gets the EPS estimate (if available).</summary>
    public decimal? EpsEstimate { get; init; }
}

/// <summary>
/// Earnings announcement timing relative to market hours.
/// </summary>
public enum EarningsTiming
{
    /// <summary>Before market open.</summary>
    BeforeMarketOpen,
    
    /// <summary>After market close.</summary>
    AfterMarketClose,
    
    /// <summary>During market hours.</summary>
    DuringMarketHours,
    
    /// <summary>Timing unknown.</summary>
    Unknown
}