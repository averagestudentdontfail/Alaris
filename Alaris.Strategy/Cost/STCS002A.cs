// =============================================================================
// STCS002A.cs - Order Parameters for Cost Calculation
// Component: STCS002A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Cost;

/// <summary>
/// Represents the parameters required for computing execution costs.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record captures all market data necessary to compute
/// realistic execution costs, including bid-ask spreads for slippage
/// estimation and premium information for tiered fee structures.
/// </para>
/// <para>
/// Reference: InteractiveBrokersFeeModel uses premium to determine fee tiers
/// for equity options (see Alaris.Lean fee model documentation).
/// </para>
/// </remarks>
public sealed record STCS002A
{
    /// <summary>
    /// Gets the number of contracts in the order.
    /// </summary>
    /// <remarks>Must be positive for valid orders.</remarks>
    public required int Contracts { get; init; }

    /// <summary>
    /// Gets the mid-price of the option.
    /// </summary>
    /// <remarks>
    /// Computed as (Bid + Ask) / 2. Used as the theoretical fill price
    /// in simulations before applying slippage.
    /// </remarks>
    public required double MidPrice { get; init; }

    /// <summary>
    /// Gets the bid price of the option.
    /// </summary>
    public required double BidPrice { get; init; }

    /// <summary>
    /// Gets the ask price of the option.
    /// </summary>
    public required double AskPrice { get; init; }

    /// <summary>
    /// Gets the order direction.
    /// </summary>
    public required OrderDirection Direction { get; init; }

    /// <summary>
    /// Gets the option premium per share (for tiered fee calculation).
    /// </summary>
    /// <remarks>
    /// For IBKR fee model: premium &lt; $0.05, $0.05 ≤ premium &lt; $0.10, premium ≥ $0.10
    /// determine fee tiers of $0.25, $0.50, $0.70 per contract respectively.
    /// </remarks>
    public required double Premium { get; init; }

    /// <summary>
    /// Gets the contract multiplier (typically 100 for equity options).
    /// </summary>
    public double ContractMultiplier { get; init; } = 100.0;

    /// <summary>
    /// Gets the underlying symbol for reference.
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Gets the bid-ask spread.
    /// </summary>
    public double BidAskSpread => AskPrice - BidPrice;

    /// <summary>
    /// Gets the half-spread (single-side slippage).
    /// </summary>
    public double HalfSpread => BidAskSpread / 2.0;

    /// <summary>
    /// Validates the order parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when validation fails.
    /// </exception>
    public void Validate()
    {
        if (Contracts <= 0)
        {
            throw new InvalidOperationException(
                $"Contract count must be positive. Received: {Contracts}");
        }

        if (MidPrice < 0)
        {
            throw new InvalidOperationException(
                $"Mid price cannot be negative. Received: {MidPrice}");
        }

        if (BidPrice < 0)
        {
            throw new InvalidOperationException(
                $"Bid price cannot be negative. Received: {BidPrice}");
        }

        if (AskPrice < BidPrice)
        {
            throw new InvalidOperationException(
                $"Ask price ({AskPrice}) cannot be less than bid price ({BidPrice}).");
        }

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            throw new InvalidOperationException("Symbol cannot be null or empty.");
        }
    }
}

/// <summary>
/// Specifies the direction of an order.
/// </summary>
public enum OrderDirection
{
    /// <summary>
    /// Buy order (long position entry or short position exit).
    /// Fills at ask price in realistic execution.
    /// </summary>
    Buy,

    /// <summary>
    /// Sell order (long position exit or short position entry).
    /// Fills at bid price in realistic execution.
    /// </summary>
    Sell
}