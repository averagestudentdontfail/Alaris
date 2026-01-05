// STCS003A.cs - execution cost result

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the computed execution cost for a single option leg.
/// </summary>

public sealed record STCS003A
{
    /// <summary>
    /// Gets the brokerage commission in dollars.
    /// </summary>
    
    public required double Commission { get; init; }

    /// <summary>
    /// Gets the exchange fees in dollars.
    /// </summary>
    
    public double ExchangeFees { get; init; }

    /// <summary>
    /// Gets the regulatory fees in dollars (e.g., SEC, FINRA).
    /// </summary>
    public double RegulatoryFees { get; init; }

    /// <summary>
    /// Gets the estimated slippage cost in dollars.
    /// </summary>
    
    public required double Slippage { get; init; }

    /// <summary>
    /// Gets the theoretical fill price (mid-price).
    /// </summary>
    public required double TheoreticalPrice { get; init; }

    /// <summary>
    /// Gets the execution-adjusted fill price.
    /// </summary>
    
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