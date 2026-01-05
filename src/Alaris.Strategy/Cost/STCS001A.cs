// STCS001A.cs - execution cost model interface

namespace Alaris.Strategy.Cost;

/// <summary>
/// Defines the contract for execution cost models.
/// </summary>

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