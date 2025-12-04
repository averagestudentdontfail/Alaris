using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alaris.Data.Provider;

/// <summary>
/// Interface for risk-free rate providers.
/// </summary>
public interface IRiskFreeRateProvider
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
