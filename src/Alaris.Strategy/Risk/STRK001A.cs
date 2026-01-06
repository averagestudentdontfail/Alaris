using Alaris.Strategy.Core;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Risk;

/// <summary>
/// Implements Kelly Criterion position sizing for options strategies.
/// Uses fractional Kelly (typically 25% of full Kelly) for safety.
/// </summary>
public sealed class STRK001A
{
    private readonly ILogger<STRK001A>? _logger;

    // LoggerMessage delegates
    private static readonly Action<ILogger, int, Exception?> LogInsufficientTradeHistory =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(1, nameof(LogInsufficientTradeHistory)),
            "Insufficient trade history ({Count} trades), using minimum position size");

    private static readonly Action<ILogger, double, Exception?> LogInvalidWinRate =
        LoggerMessage.Define<double>(
            LogLevel.Warning,
            new EventId(2, nameof(LogInvalidWinRate)),
            "Invalid win rate {WinRate:P2}, using minimum position size");

    private static readonly Action<ILogger, Exception?> LogInvalidAverageWinLoss =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3, nameof(LogInvalidAverageWinLoss)),
            "Invalid average win/loss, using minimum position size");

    private static readonly Action<ILogger, string, int, double, double, Exception?> LogPositionCalculated =
        LoggerMessage.Define<string, int, double, double>(
            LogLevel.Information,
            new EventId(4, nameof(LogPositionCalculated)),
            "Position calculated for {Symbol}: {Contracts} contracts, {Allocation:P2} allocation (Kelly={Kelly:P2})");

    private static readonly Action<ILogger, string, Exception?> LogErrorCalculatingPosition =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5, nameof(LogErrorCalculatingPosition)),
            "Error calculating position size for {Symbol}");

    private const double FractionalKelly = 0.25; // Use 25% of full Kelly for safety
    private const double MaxAllocation = 0.06;   // Cap at 6% of portfolio
    private const double ContractMultiplier = 100.0;

    public STRK001A(ILogger<STRK001A>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates position size based on historical trade performance.
    /// Kelly Formula: f* = (p*b - q) / b
    /// where p = win probability, q = loss probability, b = win/loss ratio
    /// </summary>
    public STRK002A CalculateFromHistory(
        double portfolioValue,
        IReadOnlyList<Trade> historicalTrades,
        double spreadCost,
        STCR004A signal)
    {
        ArgumentNullException.ThrowIfNull(historicalTrades);
        ArgumentNullException.ThrowIfNull(signal);

        if (portfolioValue <= 0)
        {
            throw new ArgumentException("Portfolio value must be positive", nameof(portfolioValue));
        }

        if (spreadCost <= 0)
        {
            throw new ArgumentException("Spread cost must be positive", nameof(spreadCost));
        }

        STRK002A positionSize = new STRK002A
        {
            MaxLossPerContract = spreadCost * ContractMultiplier
        };

        // Need sufficient trade history for meaningful statistics
        if (historicalTrades.Count < 20)
        {
            SafeLog(() => LogInsufficientTradeHistory(_logger!, historicalTrades.Count, null));
            return GetMinimumPosition(portfolioValue, spreadCost);
        }

        try
        {
            // Calculate win rate
            int winCount = 0;
            int lossCount = 0;
            double winSum = 0.0;
            double lossSum = 0.0;

            for (int i = 0; i < historicalTrades.Count; i++)
            {
                Trade trade = historicalTrades[i];
                if (trade.ProfitLoss > 0)
                {
                    winCount++;
                    winSum += trade.ProfitLoss;
                }
                else
                {
                    lossCount++;
                    lossSum += trade.ProfitLoss;
                }
            }

            double winRate = (double)winCount / historicalTrades.Count;
            double lossRate = 1 - winRate;

            if (winRate <= 0 || winRate >= 1)
            {
                SafeLog(() => LogInvalidWinRate(_logger!, winRate, null));
                return GetMinimumPosition(portfolioValue, spreadCost);
            }

            // Calculate average win and loss amounts
            double avgWin = winCount > 0 ? winSum / winCount : 0;
            double avgLoss = lossCount > 0
                ? Math.Abs(lossSum / lossCount)
                : (spreadCost * ContractMultiplier);

            if (avgWin <= 0 || avgLoss <= 0)
            {
                SafeLog(() => LogInvalidAverageWinLoss(_logger!, null));
                return GetMinimumPosition(portfolioValue, spreadCost);
            }

            double winLossRatio = avgWin / avgLoss;

            // Kelly formula: f* = (p*b - q) / b
            double fullKellyPercent = ((winRate * winLossRatio) - lossRate) / winLossRatio;

            // Apply fractional Kelly for safety
            double kellyPercent = fullKellyPercent * FractionalKelly;

            // Cap at maximum allocation
            double allocationPercent = Math.Max(0, Math.Min(kellyPercent, MaxAllocation));

            // Adjust based on signal strength
            allocationPercent = AdjustForSTCR004AStrength(allocationPercent, signal);

            // Calculate position size
            double dollarAllocation = portfolioValue * allocationPercent;
            int contracts = (int)Math.Floor(dollarAllocation / (spreadCost * ContractMultiplier));

            positionSize.Contracts = Math.Max(contracts, 0);
            positionSize.AllocationPercent = allocationPercent;
            positionSize.DollarAllocation = dollarAllocation;
            positionSize.TotalRisk = contracts * spreadCost * ContractMultiplier;
            positionSize.ExpectedProfitPerContract = avgWin;
            positionSize.KellyFraction = fullKellyPercent;

            SafeLog(() => LogPositionCalculated(_logger!, signal.Symbol, positionSize.Contracts, allocationPercent, fullKellyPercent, null));

            return positionSize;
        }
        catch (DivideByZeroException ex)
        {
            SafeLog(() => LogErrorCalculatingPosition(_logger!, signal.Symbol, ex));
            throw;
        }
        catch (OverflowException ex)
        {
            SafeLog(() => LogErrorCalculatingPosition(_logger!, signal.Symbol, ex));
            throw;
        }
        catch (InvalidOperationException ex)
        {
            SafeLog(() => LogErrorCalculatingPosition(_logger!, signal.Symbol, ex));
            throw;
        }
    }

    /// <summary>
    /// Calculates Kelly fraction adjusted for transaction costs.
    /// </summary>
    
    /// <param name="winProbability">Historical win probability (0-1).</param>
    /// <param name="avgWin">Average winning trade amount.</param>
    /// <param name="avgLoss">Average losing trade amount (positive).</param>
    /// <param name="roundTripCost">Round-trip transaction cost per contract.</param>
    /// <returns>Net-of-cost Kelly fraction.</returns>
    public static double CalculateNetOfCostKelly(
        double winProbability,
        double avgWin,
        double avgLoss,
        double roundTripCost)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(avgWin);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(avgLoss);
        ArgumentOutOfRangeException.ThrowIfNegative(roundTripCost);

        if (winProbability <= 0 || winProbability >= 1)
        {
            return 0.0;
        }

        double lossProbability = 1.0 - winProbability;

        // Adjust win amount for transaction costs
        double effectiveWin = avgWin - roundTripCost;
        if (effectiveWin <= 0)
        {
            return 0.0; // Costs exceed edge - no positive expectancy
        }

        // Adjusted win/loss ratio
        double effectiveWinLossRatio = effectiveWin / avgLoss;

        // Modified Kelly formula with costs
        double kellyFraction = ((winProbability * effectiveWinLossRatio) - lossProbability) / effectiveWinLossRatio;

        // Never bet if negative expectancy
        return Math.Max(0.0, kellyFraction);
    }

    /// <summary>
    /// Adjusts allocation based on signal strength.
    /// </summary>
    private static double AdjustForSTCR004AStrength(double baseAllocation, STCR004A signal)
    {
        return signal.Strength switch
        {
            STCR004AStrength.Recommended => baseAllocation * 1.0,  // Full allocation
            STCR004AStrength.Consider => baseAllocation * 0.5,      // Half allocation
            STCR004AStrength.Avoid => 0.0,                          // No allocation
            _ => 0.0
        };
    }

    /// <summary>
    /// Returns a minimum safe position size.
    /// </summary>
    private static STRK002A GetMinimumPosition(double portfolioValue, double spreadCost)
    {
        const double minAllocation = 0.01; // 1% minimum
        double dollarAllocation = portfolioValue * minAllocation;
        int contracts = (int)Math.Floor(dollarAllocation / (spreadCost * ContractMultiplier));

        return new STRK002A
        {
            Contracts = Math.Max(contracts, 1),
            AllocationPercent = minAllocation,
            DollarAllocation = dollarAllocation,
            MaxLossPerContract = spreadCost * ContractMultiplier,
            TotalRisk = contracts * spreadCost * ContractMultiplier,
            KellyFraction = minAllocation
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
            // This is acceptable per Rule 10 for non-critical subsystems (Rule 15: Fault Isolation)
        }
#pragma warning restore CA1031
    }
}

/// <summary>
/// Represents a historical trade for Kelly Criterion calculation.
/// </summary>
public sealed class Trade
{
    /// <summary>
    /// Gets the trade entry date.
    /// </summary>
    public DateTime EntryDate { get; init; }

    /// <summary>
    /// Gets the trade exit date.
    /// </summary>
    public DateTime ExitDate { get; init; }

    /// <summary>
    /// Gets the profit or loss (negative for loss).
    /// </summary>
    public double ProfitLoss { get; init; }

    /// <summary>
    /// Gets the underlying symbol.
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Gets the trade type/strategy.
    /// </summary>
    public string Strategy { get; init; } = string.Empty;

    /// <summary>
    /// Gets the holding period in days.
    /// </summary>
    public int HoldingPeriod => (ExitDate.Date - EntryDate.Date).Days;

    /// <summary>
    /// Gets whether the trade was profitable.
    /// </summary>
    public bool IsWinner => ProfitLoss > 0;
}
