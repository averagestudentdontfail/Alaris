using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Data.Model;

namespace Alaris.Data.Provider;

/// <summary>
/// Interface for market data providers (Polygon, IBKR, etc.).
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Gets historical OHLCV bars for a symbol.
    /// </summary>
    Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current options chain for a symbol.
    /// </summary>
    Task<OptionChainSnapshot> GetOptionChainAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current spot price for a symbol.
    /// </summary>
    Task<decimal> GetSpotPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets 30-day average volume for a symbol.
    /// </summary>
    Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}
