// DTpr004A.cs - Earnings calendar provider interface

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Provider;

/// <summary>
/// Interface for earnings calendar providers (FMP, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Component ID: DTpr004A
/// </para>
/// <para>
/// Implementations provide upcoming and historical earnings events,
/// including announcement dates, EPS estimates, and fiscal period data.
/// </para>
/// </remarks>
public interface DTpr004A
{
    /// <summary>
    /// Gets upcoming earnings events for a symbol.
    /// </summary>
    Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(
        string symbol,
        int daysAhead = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical earnings events for a symbol.
    /// </summary>
    Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical earnings events for a symbol, anchored to a specific date.
    /// Use in backtest mode with simulation date as anchor.
    /// </summary>
    Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        DateTime anchorDate,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all symbols with earnings in date range.
    /// </summary>
    Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables cache-only mode (disables API calls). Call for backtests.
    /// </summary>
    void EnableCacheOnlyMode();
}
