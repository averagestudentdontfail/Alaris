// =============================================================================
// STCS003A.cs - Execution Cost Result
// Component: STCS003A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the computed execution cost for a single option leg.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record decomposes execution costs into their constituent
/// components: commission fees, exchange fees, regulatory fees, and slippage.
/// This granularity enables detailed cost attribution and model validation.
/// </para>
/// <para>
/// Total cost = Commission + ExchangeFees + RegulatoryFees + Slippage
/// </para>
/// </remarks>
public sealed record STCS003A
{
    /// <summary>
    /// Gets the brokerage commission in dollars.
    /// </summary>
    /// <remarks>
    /// For IBKR equity options, this varies by monthly volume tier:
    /// ≤10K contracts: $0.25-$0.70/contract depending on premium.
    /// </remarks>
    public required double Commission { get; init; }

    /// <summary>
    /// Gets the exchange fees in dollars.
    /// </summary>
    /// <remarks>
    /// Exchange fees are charged by the options exchange (e.g., CBOE, NYSE Arca).
    /// These are typically passed through by the broker.
    /// </remarks>
    public double ExchangeFees { get; init; }

    /// <summary>
    /// Gets the regulatory fees in dollars (e.g., SEC, FINRA).
    /// </summary>
    public double RegulatoryFees { get; init; }

    /// <summary>
    /// Gets the estimated slippage cost in dollars.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slippage represents the cost of crossing the bid-ask spread.
    /// For buy orders: fill at ask (pay half-spread).
    /// For sell orders: fill at bid (receive half-spread less).
    /// </para>
    /// <para>
    /// Computed as: HalfSpread × Contracts × ContractMultiplier
    /// </para>
    /// </remarks>
    public required double Slippage { get; init; }

    /// <summary>
    /// Gets the theoretical fill price (mid-price).
    /// </summary>
    public required double TheoreticalPrice { get; init; }

    /// <summary>
    /// Gets the execution-adjusted fill price.
    /// </summary>
    /// <remarks>
    /// For buy orders: Ask price (or mid + half-spread).
    /// For sell orders: Bid price (or mid - half-spread).
    /// </remarks>
    public required double ExecutionPrice { get; init; }

    /// <summary>
    /// Gets the number of contracts.
    /// </summary>
    public required int Contracts { get; init; }

    /// <summary>
    /// Gets the total execution cost (all components).
    /// </summary>
    public double TotalCost => Commission + ExchangeFees + RegulatoryFees + Slippage;

    /// <summary>
    /// Gets the cost per contract.
    /// </summary>
    public double CostPerContract => Contracts > 0 ? TotalCost / Contracts : 0.0;

    /// <summary>
    /// Gets the slippage as a percentage of theoretical value.
    /// </summary>
    public double SlippagePercent => TheoreticalPrice > 0
        ? Slippage / (TheoreticalPrice * Contracts * 100.0) * 100.0
        : 0.0;

    /// <summary>
    /// Gets the total fees (excluding slippage).
    /// </summary>
    public double TotalFees => Commission + ExchangeFees + RegulatoryFees;
}