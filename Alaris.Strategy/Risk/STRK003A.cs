// =============================================================================
// STRK003A.cs - Concurrent Position Reserve Manager
// Component: STRK003A | Category: Risk | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.4
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using Alaris.Strategy.Core;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Risk;

/// <summary>
/// Implements reserve-adjusted Kelly position sizing for concurrent positions.
/// </summary>
/// <remarks>
/// <para>
/// During earnings season clustering (Feb/Apr/Jul/Oct), 15-20 signals may
/// arrive within a week. This component adjusts Kelly allocations to reserve
/// capital for subsequent opportunities, preventing capital starvation.
/// </para>
/// <para>
/// Key mechanisms:
/// - Little's Law estimation of expected concurrent positions
/// - Reserve buffer for variance in position clustering
/// - Dynamic quality thresholds when capital utilisation is high
/// </para>
/// <para>
/// Reference: Thorp (2006) "The Kelly Criterion in Blackjack Sports Betting
/// and the Stock Market" - simultaneous Kelly betting framework.
/// </para>
/// </remarks>
public sealed class STRK003A
{
    private readonly STRK001A _baseKelly;
    private readonly ILogger<STRK003A>? _logger;

    // Default reserve buffer (1.3x accounts for ~1 std dev of clustering variance)
    private const double DefaultReserveBuffer = 1.30;

    // Utilisation threshold above which we raise quality bar
    private const double HighUtilisationThreshold = 0.75;

    // Utilisation threshold above which we stop taking new positions
    private const double MaxUtilisationThreshold = 0.90;

    // LoggerMessage delegates
    private static readonly Action<ILogger, double, double, double, Exception?> LogReserveCalculation =
        LoggerMessage.Define<double, double, double>(
            LogLevel.Information,
            new EventId(1, nameof(LogReserveCalculation)),
            "Reserve calculation: arrivalRate={ArrivalRate:F3}/day, holdingPeriod={HoldingPeriod:F1}d, expectedConcurrent={ExpectedConcurrent:F1}");

    private static readonly Action<ILogger, double, string, Exception?> LogUtilisationFiltered =
        LoggerMessage.Define<double, string>(
            LogLevel.Information,
            new EventId(2, nameof(LogUtilisationFiltered)),
            "Position filtered: utilisation={Utilisation:P0} exceeds threshold, signalStrength={Strength}");

    private static readonly Action<ILogger, string, double, double, Exception?> LogAdjustedAllocation =
        LoggerMessage.Define<string, double, double>(
            LogLevel.Information,
            new EventId(3, nameof(LogAdjustedAllocation)),
            "Reserve-adjusted allocation for {Symbol}: base={BaseAlloc:P2}, adjusted={AdjustedAlloc:P2}");

    /// <summary>
    /// Initialises a new instance of the concurrent position reserve manager.
    /// </summary>
    /// <param name="baseKelly">Base Kelly calculator for individual position sizing.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when baseKelly is null.</exception>
    public STRK003A(STRK001A baseKelly, ILogger<STRK003A>? logger = null)
    {
        _baseKelly = baseKelly ?? throw new ArgumentNullException(nameof(baseKelly));
        _logger = logger;
    }

    /// <summary>
    /// Calculates expected concurrent positions using Little's Law.
    /// </summary>
    /// <param name="arrivalRate">Average number of signals per trading day.</param>
    /// <param name="averageHoldingPeriod">Average holding period in trading days.</param>
    /// <returns>Expected number of concurrent positions.</returns>
    /// <remarks>
    /// Little's Law: L = λ × W, where L is average number in system,
    /// λ is arrival rate, and W is average time in system.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when arrivalRate or averageHoldingPeriod is non-positive.
    /// </exception>
    public double CalculateExpectedConcurrent(double arrivalRate, double averageHoldingPeriod)
    {
        if (arrivalRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrivalRate),
                "Arrival rate must be positive");
        }

        if (averageHoldingPeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(averageHoldingPeriod),
                "Holding period must be positive");
        }

        double expectedConcurrent = arrivalRate * averageHoldingPeriod;

        SafeLog(() => LogReserveCalculation(_logger!, arrivalRate, averageHoldingPeriod, expectedConcurrent, null));

        return expectedConcurrent;
    }

    /// <summary>
    /// Calculates expected concurrent positions from historical trade data.
    /// </summary>
    /// <param name="historicalTrades">Historical trades to analyse.</param>
    /// <param name="analysisWindowDays">Number of trading days to analyse.</param>
    /// <returns>Expected number of concurrent positions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when historicalTrades is null.</exception>
    /// <exception cref="ArgumentException">Thrown when insufficient trade history.</exception>
    public double CalculateExpectedConcurrentFromHistory(
        IReadOnlyList<Trade> historicalTrades,
        int analysisWindowDays = 252)
    {
        ArgumentNullException.ThrowIfNull(historicalTrades);

        if (historicalTrades.Count < 10)
        {
            throw new ArgumentException("Requires at least 10 trades for reliable estimation",
                nameof(historicalTrades));
        }

        // Calculate arrival rate (trades per day)
        DateTime earliest = historicalTrades.Min(t => t.EntryDate);
        DateTime latest = historicalTrades.Max(t => t.EntryDate);
        int tradingDays = Math.Max(1, (int)(latest - earliest).TotalDays);
        double arrivalRate = (double)historicalTrades.Count / tradingDays;

        // Calculate average holding period
        double avgHoldingPeriod = historicalTrades.Average(t => t.HoldingPeriod);

        return CalculateExpectedConcurrent(arrivalRate, avgHoldingPeriod);
    }

    /// <summary>
    /// Calculates position size with concurrent position reserve adjustment.
    /// </summary>
    /// <param name="portfolioValue">Total portfolio value.</param>
    /// <param name="historicalTrades">Historical trades for Kelly calculation.</param>
    /// <param name="spreadCost">Cost of the calendar spread.</param>
    /// <param name="signal">Trading signal.</param>
    /// <param name="currentOpenPositions">Number of currently open positions.</param>
    /// <param name="expectedConcurrent">Expected concurrent positions (from Little's Law).</param>
    /// <param name="reserveBuffer">Buffer multiplier for clustering variance (default: 1.3).</param>
    /// <returns>Reserve-adjusted position size.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when numeric parameters are invalid.</exception>
    public STRK002A CalculateWithReserve(
        double portfolioValue,
        IReadOnlyList<Trade> historicalTrades,
        double spreadCost,
        STCR004A signal,
        int currentOpenPositions,
        double expectedConcurrent,
        double reserveBuffer = DefaultReserveBuffer)
    {
        ArgumentNullException.ThrowIfNull(historicalTrades);
        ArgumentNullException.ThrowIfNull(signal);

        if (portfolioValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolioValue),
                "Portfolio value must be positive");
        }

        if (spreadCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spreadCost),
                "Spread cost must be positive");
        }

        if (currentOpenPositions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentOpenPositions),
                "Open positions cannot be negative");
        }

        if (expectedConcurrent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedConcurrent),
                "Expected concurrent positions must be positive");
        }

        if (reserveBuffer < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(reserveBuffer),
                "Reserve buffer must be at least 1.0");
        }

        // Calculate capital utilisation
        double targetCapacity = expectedConcurrent * reserveBuffer;
        double utilisation = currentOpenPositions / targetCapacity;

        // Dynamic quality threshold based on utilisation
        if (utilisation >= MaxUtilisationThreshold)
        {
            SafeLog(() => LogUtilisationFiltered(_logger!, utilisation, signal.Strength.ToString(), null));
            return GetZeroPosition();
        }

        if (utilisation >= HighUtilisationThreshold &&
            signal.Strength != STCR004AStrength.Recommended)
        {
            SafeLog(() => LogUtilisationFiltered(_logger!, utilisation, signal.Strength.ToString(), null));
            return GetZeroPosition();
        }

        // Get base Kelly allocation
        STRK002A basePosition = _baseKelly.CalculateFromHistory(
            portfolioValue, historicalTrades, spreadCost, signal);

        if (basePosition.Contracts == 0)
        {
            return basePosition;
        }

        // Apply reserve adjustment
        double adjustedAllocationPercent = basePosition.AllocationPercent / targetCapacity;

        // Ensure we don't allocate more than the base Kelly suggests
        adjustedAllocationPercent = Math.Min(adjustedAllocationPercent, basePosition.AllocationPercent);

        // Recalculate contracts based on adjusted allocation
        double dollarAllocation = portfolioValue * adjustedAllocationPercent;
        int contracts = (int)Math.Floor(dollarAllocation / (spreadCost * 100.0));

        SafeLog(() => LogAdjustedAllocation(_logger!, signal.Symbol,
            basePosition.AllocationPercent, adjustedAllocationPercent, null));

        return new STRK002A
        {
            Contracts = Math.Max(contracts, 0),
            AllocationPercent = adjustedAllocationPercent,
            DollarAllocation = dollarAllocation,
            MaxLossPerContract = basePosition.MaxLossPerContract,
            TotalRisk = contracts * basePosition.MaxLossPerContract,
            ExpectedProfitPerContract = basePosition.ExpectedProfitPerContract,
            KellyFraction = basePosition.KellyFraction
        };
    }

    /// <summary>
    /// Returns a zero-size position (filtered by utilisation threshold).
    /// </summary>
    private static STRK002A GetZeroPosition()
    {
        return new STRK002A
        {
            Contracts = 0,
            AllocationPercent = 0.0,
            DollarAllocation = 0.0,
            MaxLossPerContract = 0.0,
            TotalRisk = 0.0,
            ExpectedProfitPerContract = 0.0,
            KellyFraction = 0.0
        };
    }

    /// <summary>
    /// Safely executes logging operation with fault isolation (Rule 15).
    /// Prevents logging failures from crashing critical paths.
    /// </summary>
    private void SafeLog(Action logAction)
    {
        if (_logger == null)
        {
            return;
        }

#pragma warning disable CA1031 // Do not catch general exception types
        try
        {
            logAction();
        }
        catch (Exception)
        {
            // Swallow logging exceptions to prevent them from crashing the application
        }
#pragma warning restore CA1031
    }
}
