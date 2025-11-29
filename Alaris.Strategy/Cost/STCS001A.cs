// =============================================================================
// STCS001A.cs - Execution Cost Model Interface
// Component: STCS001A | Category: Cost | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.2
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

namespace Alaris.Strategy.Cost;

/// <summary>
/// Defines the contract for execution cost models.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the computation of transaction costs, enabling
/// pluggable implementations ranging from simple constant fees to sophisticated
/// brokerage-specific models (e.g., InteractiveBrokersFeeModel via Alaris.Lean).
/// </para>
/// <para>
/// Implementations must compute costs deterministically given the same inputs,
/// supporting reproducible backtesting and strategy validation.
/// </para>
/// </remarks>
public interface STCS001A
{
    /// <summary>
    /// Gets the name of the cost model for logging and identification.
    /// </summary>
    public string ModelName { get; }

    /// <summary>
    /// Computes the execution cost for an option order.
    /// </summary>
    /// <param name="parameters">The order parameters.</param>
    /// <returns>The computed execution cost result.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="parameters"/> is null.
    /// </exception>
    public STCS003A ComputeOptionCost(STCS002A parameters);

    /// <summary>
    /// Computes the execution cost for a calendar spread order.
    /// </summary>
    /// <param name="frontLegParameters">Parameters for the front-month leg.</param>
    /// <param name="backLegParameters">Parameters for the back-month leg.</param>
    /// <returns>The combined execution cost for the spread.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when either parameter is null.
    /// </exception>
    public STCS004A ComputeSpreadCost(STCS002A frontLegParameters, STCS002A backLegParameters);
}