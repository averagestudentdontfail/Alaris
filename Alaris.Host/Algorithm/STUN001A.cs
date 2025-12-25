// =============================================================================
// STUN001A.cs - Earnings Universe Selection Model
// Component: STUN001A | Category: Universe | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Reference: QuantConnect.Algorithm.Framework.Selection
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Provider;

namespace Alaris.Host.Algorithm.Universe;

/// <summary>
/// Universe selection model that filters for symbols with upcoming earnings.
/// Implements Atilgan (2014) pre-screening criteria.
/// </summary>
/// <remarks>
/// <para>
/// Selection criteria (from Atilgan 2014):
/// - Minimum 30-day average dollar volume: $1.5M
/// - Minimum price: $5.00
/// - Earnings announcement in T+[5,7] days
/// - Options available with sufficient liquidity
/// </para>
/// <para>
/// The universe refreshes daily at market open, querying the earnings
/// calendar provider for symbols with announcements in the target window.
/// </para>
/// </remarks>
public sealed class STUN001A : FundamentalUniverseSelectionModel
{
    private readonly DTpr004A _earningsProvider;
    private readonly ILogger<STUN001A>? _logger;
    
    // Atilgan (2014) parameters
    private readonly int _daysBeforeEarningsMin;
    private readonly int _daysBeforeEarningsMax;
    private readonly decimal _minimumDollarVolume;
    private readonly decimal _minimumPrice;
    private readonly int _maxSymbols;
    
    // Cache to avoid redundant API calls
    private DateTime _lastEarningsQueryDate;
    private HashSet<string> _cachedEarningsSymbols = new();
    
    // LoggerMessage delegates
    private static readonly Action<ILogger, DateTime, DateTime, int, Exception?> LogEarningsQuery =
        LoggerMessage.Define<DateTime, DateTime, int>(
            LogLevel.Information,
            new EventId(1, nameof(LogEarningsQuery)),
            "Querying earnings calendar from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}, found {Count} symbols");
    
    private static readonly Action<ILogger, int, int, int, Exception?> LogUniverseSelection =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogUniverseSelection)),
            "Universe selection: {CoarseCount} coarse → {EarningsFiltered} with earnings → {Final} final");

    /// <summary>
    /// Initialises a new instance of the earnings universe selection model.
    /// </summary>
    /// <param name="earningsProvider">Provider for earnings calendar data.</param>
    /// <param name="daysBeforeEarningsMin">Minimum days before earnings (default: 5).</param>
    /// <param name="daysBeforeEarningsMax">Maximum days before earnings (default: 7).</param>
    /// <param name="minimumDollarVolume">Minimum 30-day dollar volume (default: 1.5M).</param>
    /// <param name="minimumPrice">Minimum share price (default: $5).</param>
    /// <param name="maxCoarseSymbols">Maximum coarse symbols to process (default: 500).</param>
    /// <param name="maxFinalSymbols">Maximum symbols to select (default: 50).</param>
    /// <param name="logger">Optional logger instance.</param>
    public STUN001A(
        DTpr004A earningsProvider,
        int daysBeforeEarningsMin = 5,
        int daysBeforeEarningsMax = 7,
        decimal minimumDollarVolume = 1_500_000m,
        decimal minimumPrice = 5.00m,
        int maxCoarseSymbols = 500,
        int maxFinalSymbols = 50,
        ILogger<STUN001A>? logger = null)
         : base()
    {
        _earningsProvider = earningsProvider
            ?? throw new ArgumentNullException(nameof(earningsProvider));
        _daysBeforeEarningsMin = daysBeforeEarningsMin;
        _daysBeforeEarningsMax = daysBeforeEarningsMax;
        _minimumDollarVolume = minimumDollarVolume;
        _minimumPrice = minimumPrice;
        _maxSymbols = maxFinalSymbols;
        _logger = logger;
    }

    /// <summary>
    /// Returns configuration summary for logging.
    /// </summary>
    public string GetConfigurationSummary()
    {
        return $"Days before earnings: [{_daysBeforeEarningsMin}, {_daysBeforeEarningsMax}], " +
               $"Min volume: {_minimumDollarVolume:C0}, Min price: {_minimumPrice:C2}, " +
               $"Max symbols: {_maxSymbols}";
    }

    /// <summary>
    /// Selects symbols from the fundamental universe that meet earnings criteria.
    /// This is the primary selection method for LEAN's universe selection.
    /// </summary>
    /// <param name="algorithm">The algorithm instance.</param>
    /// <param name="fundamental">The fundamental data for all symbols.</param>
    /// <returns>Symbols meeting the earnings criteria.</returns>
    public override IEnumerable<Symbol> Select(QCAlgorithm algorithm, IEnumerable<Fundamental> fundamental)
    {
        var currentDate = algorithm.Time.Date;
        
        // Step 1: Apply fundamental filters (volume, price)
        var filtered = fundamental
            .Where(f => f.HasFundamentalData)
            .Where(f => f.DollarVolume >= (double)_minimumDollarVolume)
            .Where(f => f.Price >= _minimumPrice)
            .OrderByDescending(f => f.DollarVolume)
            .Take(500) // Pre-filter to top 500 by volume
            .ToList();
        
        // Step 2: Query earnings calendar (with caching)
        var earningsSymbols = GetEarningsSymbolsForWindow(
            algorithm,
            currentDate.AddDays(_daysBeforeEarningsMin),
            currentDate.AddDays(_daysBeforeEarningsMax));
        
        // Step 3: Filter to symbols with upcoming earnings
        var selected = filtered
            .Where(f => earningsSymbols.Contains(f.Symbol.Value))
            .Take(_maxSymbols)
            .Select(f => f.Symbol)
            .ToList();
        
        if (_logger != null)
        {
            LogUniverseSelection(_logger, filtered.Count, earningsSymbols.Count, selected.Count, null);
        }
        
        return selected;
    }

    /// <summary>
    /// Selects symbols from the coarse universe that meet earnings criteria.
    /// </summary>
    [Obsolete("Use Select instead")]
    public override IEnumerable<Symbol> SelectCoarse(
        QCAlgorithm algorithm,
        IEnumerable<CoarseFundamental> coarse)
    {
        var currentDate = algorithm.Time.Date;
        
        // Step 1: Apply coarse filters (volume, price, has fundamental data)
        var coarseFiltered = coarse
            .Where(c => c.HasFundamentalData)
            .Where(c => c.DollarVolume >= (double)_minimumDollarVolume)
            .Where(c => c.Price >= _minimumPrice)
            .OrderByDescending(c => c.DollarVolume)
            .Take(500) // Pre-filter to top 500 by volume
            .ToList();
        
        // Step 2: Query earnings calendar (with caching)
        var earningsSymbols = GetEarningsSymbolsForWindow(
            algorithm,
            currentDate.AddDays(_daysBeforeEarningsMin),
            currentDate.AddDays(_daysBeforeEarningsMax));
        
        // Step 3: Filter to symbols with upcoming earnings
        var selected = coarseFiltered
            .Where(c => earningsSymbols.Contains(c.Symbol.Value))
            .Take(_maxSymbols)
            .Select(c => c.Symbol)
            .ToList();
        
        if (_logger != null)
        {
            LogUniverseSelection(_logger, coarseFiltered.Count, earningsSymbols.Count, selected.Count, null);
        }
        
        return selected;
    }

    /// <summary>
    /// Optional fine selection - not used for this strategy.
    /// </summary>
    [Obsolete("Use Select instead")]
    public override IEnumerable<Symbol> SelectFine(
        QCAlgorithm algorithm,
        IEnumerable<FineFundamental> fine)
    {
        // No fine filtering needed - we use earnings calendar instead
        return fine.Select(f => f.Symbol);
    }

    /// <summary>
    /// Gets symbols with earnings in the specified date window.
    /// </summary>
    private HashSet<string> GetEarningsSymbolsForWindow(
        QCAlgorithm algorithm,
        DateTime startDate,
        DateTime endDate)
    {
        // Cache earnings query for same day
        if (_lastEarningsQueryDate == algorithm.Time.Date)
        {
            return _cachedEarningsSymbols;
        }
        
        try
        {
            // Query earnings provider (synchronous for LEAN compatibility)
            var symbols = _earningsProvider.GetSymbolsWithEarningsAsync(
                startDate,
                endDate,
                CancellationToken.None)
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
        catch (Exception ex)
        {
            algorithm.Error($"Failed to query earnings calendar: {ex.Message}");
            return _cachedEarningsSymbols; // Return cached if query fails
        }
    }
}
