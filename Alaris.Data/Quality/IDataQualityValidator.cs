using System;
using Alaris.Data.Model;

namespace Alaris.Data.Quality;

/// <summary>
/// Interface for data quality validators.
/// </summary>
public interface IDataQualityValidator
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
