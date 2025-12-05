// =============================================================================
// DTqc002A.cs - Data Quality Validator Interface
// Component: DTqc002A | Category: Quality | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.3.3
// Compliance: High-Integrity Coding Standard v1.2
// =============================================================================

using System;
using Alaris.Data.Model;

namespace Alaris.Data.Quality;

/// <summary>
/// Interface for data quality validators.
/// </summary>
/// <remarks>
/// <para>
/// Component ID: DTqc002A
/// </para>
/// <para>
/// Implementations validate market data snapshots against quality rules
/// such as price reasonableness, IV arbitrage detection, volume/OI checks,
/// and earnings date verification.
/// </para>
/// </remarks>
public interface DTqc002A
{
    /// <summary>Gets the validator component ID (e.g., "DTqc001A").</summary>
    string ComponentId { get; }

    /// <summary>
    /// Validates market data snapshot.
    /// </summary>
    /// <param name="snapshot">The market data to validate.</param>
    /// <returns>Validation result.</returns>
    DataQualityResult Validate(MarketDataSnapshot snapshot);
}
