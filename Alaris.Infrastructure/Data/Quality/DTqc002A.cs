// DTqc002A.cs - Data quality validator interface

using System;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Quality;

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
