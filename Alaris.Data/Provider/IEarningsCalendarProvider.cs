using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Data.Model;

namespace Alaris.Data.Provider;

/// <summary>
/// Interface for earnings calendar providers (FMP, etc.).
/// </summary>
public interface IEarningsCalendarProvider
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
    /// Gets all symbols with earnings in date range.
    /// </summary>
    Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
