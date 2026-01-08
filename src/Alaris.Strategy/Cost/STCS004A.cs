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
    
    public required decimal TheoreticalDebit { get; init; }

    /// <summary>
    /// Gets the execution-adjusted spread debit.
    /// </summary>
    
    public required decimal ExecutionDebit { get; init; }

    /// <summary>
    /// Gets the number of spread contracts.
    /// </summary>
    public required int Contracts { get; init; }

    /// <summary>
    /// Gets the contract multiplier (typically 100).
    /// </summary>
    public decimal ContractMultiplier { get; init; } = 100.0m;

    /// <summary>
    /// Gets the total slippage across both legs.
    /// </summary>
    public decimal TotalSlippage => FrontLegCost.Slippage + BackLegCost.Slippage;

    /// <summary>
    /// Gets the total commissions across both legs.
    /// </summary>
    public decimal TotalCommission => FrontLegCost.Commission + BackLegCost.Commission;

    /// <summary>
    /// Gets the total fees across both legs.
    /// </summary>
    public decimal TotalFees => FrontLegCost.TotalFees + BackLegCost.TotalFees;

    /// <summary>
    /// Gets the total execution cost (fees + slippage).
    /// </summary>
    public decimal TotalExecutionCost => FrontLegCost.TotalCost + BackLegCost.TotalCost;

    /// <summary>
    /// Gets the cost per spread.
    /// </summary>
    public decimal CostPerSpread => Contracts > 0 ? TotalExecutionCost / Contracts : 0.0m;

    /// <summary>
    /// Gets the slippage per spread (total slippage divided by contracts).
    /// </summary>
    public decimal SlippagePerSpread => Contracts > 0 ? TotalSlippage / Contracts : 0.0m;

    /// <summary>
    /// Minimum debit used as a slippage percentage basis.
    /// </summary>
    public const decimal MinimumDebitForPercent = 0.10m;

    /// <summary>
    /// Gets the slippage as a percentage of theoretical debit.
    /// Uses a minimum debit basis to avoid extreme percentages on tiny debits.
    /// </summary>
    public decimal SlippagePercent
    {
        get
        {
            decimal debitMagnitude = Math.Abs(TheoreticalDebit);
            if (debitMagnitude <= 0.0m)
            {
                return 0.0m;
            }

            decimal basis = Math.Max(debitMagnitude, MinimumDebitForPercent);
            return Math.Abs(ExecutionDebit - TheoreticalDebit) / basis * 100.0m;
        }
    }

    /// <summary>
    /// Gets the total dollar amount required to enter the position.
    /// </summary>
    
    public decimal TotalCapitalRequired =>
        (ExecutionDebit * Contracts * ContractMultiplier) + TotalFees;

    /// <summary>
    /// Gets the theoretical capital requirement (mid-price, no fees).
    /// </summary>
    public decimal TheoreticalCapitalRequired =>
        TheoreticalDebit * Contracts * ContractMultiplier;

    /// <summary>
    /// Gets the execution cost as a percentage of theoretical capital.
    /// </summary>
    
    public decimal ExecutionCostPercent => TheoreticalCapitalRequired > 0m
        ? TotalExecutionCost / TheoreticalCapitalRequired * 100.0m
        : 0.0m;
}
