using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alaris.Data.Model;

namespace Alaris.Data.Provider;

/// <summary>
/// Interface for market data providers (Polygon, IBKR, etc.).
/// Component ID: Interface specification
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Gets historical OHLCV bars for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol to retrieve.</param>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of price bars.</returns>
    Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current options chain for a symbol.
    /// </summary>
    /// <param name="symbol">The underlying symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Option chain snapshot.</returns>
    Task<OptionChainSnapshot> GetOptionChainAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current spot price for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current spot price.</returns>
    Task<decimal> GetSpotPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets 30-day average volume for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>30-day average volume.</returns>
    Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for earnings calendar providers (FMP, etc.).
/// Component ID: Interface specification
/// </summary>
public interface IEarningsCalendarProvider
{
    /// <summary>
    /// Gets upcoming earnings events for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="daysAhead">Number of days ahead to look.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of upcoming earnings events.</returns>
    Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(
        string symbol,
        int daysAhead = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical earnings events for a symbol.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="lookbackDays">Number of days to look back.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of historical earnings events.</returns>
    Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        int lookbackDays = 730, // ~2 years
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all symbols with earnings in date range.
    /// </summary>
    /// <param name="startDate">Start date.</param>
    /// <param name="endDate">End date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of symbols with earnings.</returns>
    Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for execution quote providers (IBKR snapshots).
/// Component ID: Interface specification
/// </summary>
public interface IExecutionQuoteProvider
{
    /// <summary>
    /// Gets a real-time snapshot quote for an option contract.
    /// </summary>
    /// <param name="underlyingSymbol">The underlying symbol.</param>
    /// <param name="strike">The strike price.</param>
    /// <param name="expiration">The expiration date.</param>
    /// <param name="right">Call or put.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Real-time option quote.</returns>
    Task<OptionContract> GetSnapshotQuoteAsync(
        string underlyingSymbol,
        decimal strike,
        DateTime expiration,
        OptionRight right,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets real-time calendar spread quote.
    /// </summary>
    /// <param name="underlyingSymbol">The underlying symbol.</param>
    /// <param name="strike">The strike price.</param>
    /// <param name="frontExpiration">Front month expiration.</param>
    /// <param name="backExpiration">Back month expiration.</param>
    /// <param name="right">Call or put.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Calendar spread quote (back - front).</returns>
    Task<CalendarSpreadQuote> GetCalendarSpreadQuoteAsync(
        string underlyingSymbol,
        decimal strike,
        DateTime frontExpiration,
        DateTime backExpiration,
        OptionRight right,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Calendar spread quote with bid/ask for the spread.
/// </summary>
public sealed class CalendarSpreadQuote
{
    /// <summary>Gets the underlying symbol.</summary>
    public required string UnderlyingSymbol { get; init; }

    /// <summary>Gets the strike.</summary>
    public required decimal Strike { get; init; }

    /// <summary>Gets the front month contract.</summary>
    public required OptionContract FrontLeg { get; init; }

    /// <summary>Gets the back month contract.</summary>
    public required OptionContract BackLeg { get; init; }

    /// <summary>Gets the spread bid (back bid - front ask).</summary>
    public required decimal SpreadBid { get; init; }

    /// <summary>Gets the spread ask (back ask - front bid).</summary>
    public required decimal SpreadAsk { get; init; }

    /// <summary>Gets the spread mid.</summary>
    public decimal SpreadMid => (SpreadBid + SpreadAsk) / 2m;

    /// <summary>Gets the spread width.</summary>
    public decimal SpreadWidth => SpreadAsk - SpreadBid;

    /// <summary>Gets the quote timestamp.</summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Interface for data quality validators.
/// Component ID: Interface specification
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

/// <summary>
/// Interface for risk-free rate providers.
/// Component ID: Interface specification
/// </summary>
public interface IRiskFreeRateProvider
{
    /// <summary>
    /// Gets the current risk-free rate (e.g., 3-month T-bill).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Risk-free rate as decimal (e.g., 0.0525 for 5.25%).</returns>
    Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical risk-free rates.
    /// </summary>
    /// <param name="startDate">Start date.</param>
    /// <param name="endDate">End date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of date to rate.</returns>
    Task<IReadOnlyDictionary<DateTime, decimal>> GetHistoricalRatesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}