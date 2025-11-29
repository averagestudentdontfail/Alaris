// =============================================================================
// STCS004A.cs - Calendar Spread Execution Cost Result
// Component: STCS004A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the combined execution cost for a calendar spread.
/// </summary>
/// <remarks>
/// <para>
/// A calendar spread consists of two legs:
/// - Front leg: Sell near-term option (receives premium, pays slippage on bid)
/// - Back leg: Buy far-term option (pays premium, pays slippage on ask)
/// </para>
/// <para>
/// The net debit/credit is adjusted by execution costs to compute the
/// true entry cost of the position.
/// </para>
/// </remarks>
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
    /// <remarks>
    /// Computed as: BackMidPrice - FrontMidPrice.
    /// Positive value indicates a debit spread.
    /// </remarks>
    public required double TheoreticalDebit { get; init; }

    /// <summary>
    /// Gets the execution-adjusted spread debit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Accounts for bid-ask slippage on both legs:
    /// - Buy back-month at ask (higher cost)
    /// - Sell front-month at bid (lower proceeds)
    /// </para>
    /// <para>
    /// ExecutionDebit = BackAsk - FrontBid
    /// </para>
    /// </remarks>
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
    /// <remarks>
    /// This metric indicates how much execution costs degrade the theoretical edge.
    /// A value exceeding 5-10% may indicate insufficient liquidity.
    /// </remarks>
    public double SlippagePercent => TheoreticalDebit > 0
        ? ((ExecutionDebit - TheoreticalDebit) / TheoreticalDebit) * 100.0
        : 0.0;

    /// <summary>
    /// Gets the total dollar amount required to enter the position.
    /// </summary>
    /// <remarks>
    /// Includes the execution-adjusted debit plus all transaction costs.
    /// This is the true capital outlay for the position.
    /// </remarks>
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
    /// <remarks>
    /// Used to assess whether the signal edge survives transaction costs.
    /// Typical threshold: &lt; 5% for viable strategies.
    /// </remarks>
    public double ExecutionCostPercent => TheoreticalCapitalRequired > 0
        ? (TotalExecutionCost / TheoreticalCapitalRequired) * 100.0
        : 0.0;
}