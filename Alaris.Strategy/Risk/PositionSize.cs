namespace Alaris.Strategy.Risk;

/// <summary>
/// Represents the sizing information for an options position.
/// </summary>
public sealed class PositionSize
{
    /// <summary>
    /// Gets or sets the number of option contracts to trade.
    /// </summary>
    public int Contracts { get; set; }

    /// <summary>
    /// Gets or sets the percentage of portfolio allocated to this position.
    /// </summary>
    public double AllocationPercent { get; set; }

    /// <summary>
    /// Gets or sets the total dollar amount allocated.
    /// </summary>
    public double DollarAllocation { get; set; }

    /// <summary>
    /// Gets or sets the maximum loss per contract.
    /// </summary>
    public double MaxLossPerContract { get; set; }

    /// <summary>
    /// Gets or sets the total risk for the position.
    /// </summary>
    public double TotalRisk { get; set; }

    /// <summary>
    /// Gets or sets the expected profit per contract.
    /// </summary>
    public double ExpectedProfitPerContract { get; set; }

    /// <summary>
    /// Gets or sets the Kelly fraction used for sizing.
    /// </summary>
    public double KellyFraction { get; set; }

    /// <summary>
    /// Gets the risk/reward ratio.
    /// </summary>
    public double RiskRewardRatio => MaxLossPerContract > 0 
        ? ExpectedProfitPerContract / MaxLossPerContract 
        : 0;

    /// <summary>
    /// Validates that the position size is reasonable.
    /// </summary>
    public void Validate(double maxAllocationPercent = 0.10)
    {
        if (Contracts < 0)
            throw new InvalidOperationException("Cannot have negative contracts");

        if (AllocationPercent > maxAllocationPercent)
            throw new InvalidOperationException(
                $"Allocation {AllocationPercent:P2} exceeds maximum {maxAllocationPercent:P2}");

        if (TotalRisk > DollarAllocation)
            throw new InvalidOperationException("Total risk cannot exceed dollar allocation");
    }
}