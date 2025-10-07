using Alaris.Strategy.Core;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy.Risk;

/// <summary>
/// Implements Kelly Criterion position sizing for options strategies.
/// Uses fractional Kelly (typically 25% of full Kelly) for safety.
/// </summary>
public sealed class KellyPositionSizer
{
    private readonly ILogger<KellyPositionSizer>? _logger;
    private const double FractionalKelly = 0.25; // Use 25% of full Kelly for safety
    private const double MaxAllocation = 0.06;   // Cap at 6% of portfolio
    private const double ContractMultiplier = 100.0;

    public KellyPositionSizer(ILogger<KellyPositionSizer>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates position size based on historical trade performance.
    /// Kelly Formula: f* = (p*b - q) / b
    /// where p = win probability, q = loss probability, b = win/loss ratio
    /// </summary>
    public PositionSize CalculateFromHistory(
        double portfolioValue,
        List<Trade> historicalTrades,
        double spreadCost,
        Signal signal)
    {
        ArgumentNullException.ThrowIfNull(historicalTrades);
        ArgumentNullException.ThrowIfNull(signal);

        if (portfolioValue <= 0)
            throw new ArgumentException("Portfolio value must be positive", nameof(portfolioValue));

        if (spreadCost <= 0)
            throw new ArgumentException("Spread cost must be positive", nameof(spreadCost));

        var positionSize = new PositionSize
        {
            MaxLossPerContract = spreadCost * ContractMultiplier
        };

        // Need sufficient trade history for meaningful statistics
        if (historicalTrades.Count < 20)
        {
            _logger?.LogWarning("Insufficient trade history ({Count} trades), using minimum position size",
                historicalTrades.Count);
            return GetMinimumPosition(portfolioValue, spreadCost);
        }

        try
        {
            // Calculate win rate
            var winningTrades = historicalTrades.Where(t => t.ProfitLoss > 0).ToList();
            var losingTrades = historicalTrades.Where(t => t.ProfitLoss <= 0).ToList();

            var winRate = (double)winningTrades.Count / historicalTrades.Count;
            var lossRate = 1 - winRate;

            if (winRate <= 0 || winRate >= 1)
            {
                _logger?.LogWarning("Invalid win rate {WinRate:P2}, using minimum position size", winRate);
                return GetMinimumPosition(portfolioValue, spreadCost);
            }

            // Calculate average win and loss amounts
            var avgWin = winningTrades.Any() ? winningTrades.Average(t => t.ProfitLoss) : 0;
            var avgLoss = losingTrades.Any() ? Math.Abs(losingTrades.Average(t => t.ProfitLoss)) : spreadCost * ContractMultiplier;

            if (avgWin <= 0 || avgLoss <= 0)
            {
                _logger?.LogWarning("Invalid average win/loss, using minimum position size");
                return GetMinimumPosition(portfolioValue, spreadCost);
            }

            var winLossRatio = avgWin / avgLoss;

            // Kelly formula: f* = (p*b - q) / b
            var fullKellyPercent = (winRate * winLossRatio - lossRate) / winLossRatio;

            // Apply fractional Kelly for safety
            var kellyPercent = fullKellyPercent * FractionalKelly;

            // Cap at maximum allocation
            var allocationPercent = Math.Max(0, Math.Min(kellyPercent, MaxAllocation));

            // Adjust based on signal strength
            allocationPercent = AdjustForSignalStrength(allocationPercent, signal);

            // Calculate position size
            var dollarAllocation = portfolioValue * allocationPercent;
            var contracts = (int)Math.Floor(dollarAllocation / (spreadCost * ContractMultiplier));

            positionSize.Contracts = Math.Max(contracts, 0);
            positionSize.AllocationPercent = allocationPercent;
            positionSize.DollarAllocation = dollarAllocation;
            positionSize.TotalRisk = contracts * spreadCost * ContractMultiplier;
            positionSize.ExpectedProfitPerContract = avgWin;
            positionSize.KellyFraction = fullKellyPercent;

            _logger?.LogInformation(
                "Position calculated for {Symbol}: {Contracts} contracts, {Allocation:P2} allocation (Kelly={Kelly:P2})",
                signal.Symbol, positionSize.Contracts, allocationPercent, fullKellyPercent);

            return positionSize;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calculating position size for {Symbol}", signal.Symbol);
            return GetMinimumPosition(portfolioValue, spreadCost);
        }
    }

    /// <summary>
    /// Adjusts allocation based on signal strength.
    /// </summary>
    private static double AdjustForSignalStrength(double baseAllocation, Signal signal)
    {
        return signal.Strength switch
        {
            SignalStrength.Recommended => baseAllocation * 1.0,  // Full allocation
            SignalStrength.Consider => baseAllocation * 0.5,      // Half allocation
            SignalStrength.Avoid => 0.0,                          // No allocation
            _ => 0.0
        };
    }

    /// <summary>
    /// Returns a minimum safe position size.
    /// </summary>
    private static PositionSize GetMinimumPosition(double portfolioValue, double spreadCost)
    {
        const double minAllocation = 0.01; // 1% minimum
        var dollarAllocation = portfolioValue * minAllocation;
        var contracts = (int)Math.Floor(dollarAllocation / (spreadCost * ContractMultiplier));

        return new PositionSize
        {
            Contracts = Math.Max(contracts, 1),
            AllocationPercent = minAllocation,
            DollarAllocation = dollarAllocation,
            MaxLossPerContract = spreadCost * ContractMultiplier,
            TotalRisk = contracts * spreadCost * ContractMultiplier,
            KellyFraction = minAllocation
        };
    }
}

/// <summary>
/// Represents a historical trade for Kelly Criterion calculation.
/// </summary>
public sealed class Trade
{
    /// <summary>
    /// Gets or sets the trade entry date.
    /// </summary>
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Gets or sets the trade exit date.
    /// </summary>
    public DateTime ExitDate { get; set; }

    /// <summary>
    /// Gets or sets the profit or loss (negative for loss).
    /// </summary>
    public double ProfitLoss { get; set; }

    /// <summary>
    /// Gets or sets the underlying symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trade type/strategy.
    /// </summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>
    /// Gets the holding period in days.
    /// </summary>
    public int HoldingPeriod => (ExitDate.Date - EntryDate.Date).Days;

    /// <summary>
    /// Gets whether the trade was profitable.
    /// </summary>
    public bool IsWinner => ProfitLoss > 0;
}