// =============================================================================
// DTpr005A.cs - Risk-Free Rate Provider Interface
// Component: DTpr005A | Category: Provider | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.3
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alaris.Data.Provider;

/// <summary>
/// Interface for risk-free rate providers.
/// </summary>
/// <remarks>
/// <para>
/// Component ID: DTpr005A
/// </para>
/// <para>
/// Implementations provide current and historical risk-free rates
/// (e.g., 3-month Treasury bill rates) for option pricing models.
/// </para>
/// </remarks>
public interface DTpr005A
{
    /// <summary>
    /// Gets the current risk-free rate (e.g., 3-month T-bill).
    /// </summary>
    Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical risk-free rates.
    /// </summary>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetHistoricalRatesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
