// DTpr003A.cs - Market data provider interface

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Infrastructure.Data.Model;

namespace Alaris.Infrastructure.Data.Provider;

/// <summary>
/// Interface for market data providers (Polygon, IBKR, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Component ID: DTpr003A
/// </para>
/// <para>
/// Implementations provide historical OHLCV bars, options chains,
/// spot prices, and volume data from various market data vendors.
/// </para>
/// </remarks>
public interface DTpr003A
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
        DateTime? asOfDate = null,
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
    /// <param name="symbol">The symbol to query.</param>
    /// <param name="evaluationDate">Optional evaluation date (use LEAN's Time for backtests, null for live trading).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        DateTime? evaluationDate = null,
        CancellationToken cancellationToken = default);
}
