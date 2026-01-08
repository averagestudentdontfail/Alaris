// STCS004A.cs - calendar spread execution cost result

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the combined execution cost for a calendar spread.
/// </summary>

public sealed record STCS004A
{
    /// <summary>
    /// Gets the execution cost for the front-month leg.
    /// </summary>
    public required STCS003A FrontLegCost { get; init; }

    /// <summary>
    /// Gets the execution cost for the back-month leg.
    /// </summary>
    public required STCS003A BackLegCost { get; init; }

    /// <summary>
    /// Gets the theoretical spread debit (mid-price based).
    /// </summary>
    
    public required double TheoreticalDebit { get; init; }

    /// <summary>
    /// Gets the execution-adjusted spread debit.
    /// </summary>
    
    public required double ExecutionDebit { get; init; }

    /// <summary>
    /// Gets the number of spread contracts.
    /// </summary>
    public required int Contracts { get; init; }

    /// <summary>
    /// Gets the contract multiplier (typically 100).
    /// </summary>
    public double ContractMultiplier { get; init; } = 100.0;

    /// <summary>
    /// Gets the total slippage across both legs.
    /// </summary>
    public double TotalSlippage => FrontLegCost.Slippage + BackLegCost.Slippage;

    /// <summary>
    /// Gets the total commissions across both legs.
    /// </summary>
    public double TotalCommission => FrontLegCost.Commission + BackLegCost.Commission;

    /// <summary>
    /// Gets the total fees across both legs.
    /// </summary>
    public double TotalFees => FrontLegCost.TotalFees + BackLegCost.TotalFees;

    /// <summary>
    /// Gets the total execution cost (fees + slippage).
    /// </summary>
    public double TotalExecutionCost => FrontLegCost.TotalCost + BackLegCost.TotalCost;

    /// <summary>
    /// Gets the cost per spread.
    /// </summary>
    public double CostPerSpread => Contracts > 0 ? TotalExecutionCost / Contracts : 0.0;

    /// <summary>
    /// Gets the slippage as a percentage of theoretical debit.
    /// </summary>
    
    public double SlippagePercent => TheoreticalDebit > 0.10
        ? (ExecutionDebit - TheoreticalDebit) / TheoreticalDebit * 100.0
        : (ExecutionDebit - TheoreticalDebit) * 100.0;  // Absolute cents for small debits

    /// <summary>
    /// Gets the total dollar amount required to enter the position.
    /// </summary>
    
    public double TotalCapitalRequired =>
        (ExecutionDebit * Contracts * ContractMultiplier) + TotalFees;

    /// <summary>
    /// Gets the theoretical capital requirement (mid-price, no fees).
    /// </summary>
    public double TheoreticalCapitalRequired =>
        TheoreticalDebit * Contracts * ContractMultiplier;

    /// <summary>
    /// Gets the execution cost as a percentage of theoretical capital.
    /// </summary>
    
    public double ExecutionCostPercent => TheoreticalCapitalRequired > 0
        ? TotalExecutionCost / TheoreticalCapitalRequired * 100.0
        : 0.0;
}