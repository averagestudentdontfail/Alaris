using Alaris.Strategy.Model;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Interface for accessing market data including option chains and historical prices.
/// Implementations might use Yahoo Finance, IEX Cloud, or other data providers.
/// </summary>
public interface STDT001A
{
    /// <summary>
    /// Retrieves the complete option chain for a symbol on a specific date.
    /// </summary>
    /// <param name="symbol">The underlying security symbol.</param>
    /// <param name="expirationDate">The date for which to retrieve the option chain.</param>
    /// <returns>The option chain with all available expiration dates and strikes.</returns>
    public STDT002A GetSTDT002A(string symbol, DateTime expirationDate);

    /// <summary>
    /// Retrieves historical OHLC price data for volatility calculations.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <param name="days">Number of historical days to retrieve.</param>
    /// <returns>List of price bars ordered chronologically.</returns>
    public IReadOnlyList<PriceBar> GetHistoricalPrices(string symbol, int days);

    /// <summary>
    /// Gets the current market price of a security.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <returns>The current price.</returns>
    public double GetCurrentPrice(string symbol);

    /// <summary>
    /// Gets upcoming earnings announcement dates.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <returns>List of earnings dates.</returns>
    public Task<IReadOnlyList<DateTime>> GetEarningsDates(string symbol);

    /// <summary>
    /// Gets historical earnings announcement dates for calibrating the L&amp;S model.
    /// Reference: Leung &amp; Santoli (2014) Section 5.2 - calibrating sigma_e.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <param name="lookbackQuarters">Number of quarters to look back (default 12 = 3 years).</param>
    /// <returns>List of historical earnings dates in descending order.</returns>
    public Task<IReadOnlyList<DateTime>> GetHistoricalEarningsDates(string symbol, int lookbackQuarters = 12);

    /// <summary>
    /// Checks if market data is available for a symbol.
    /// </summary>
    public Task<bool> IsDataAvailable(string symbol);
}

/// <summary>
/// Represents a single bar of OHLCV price data.
/// </summary>
public sealed class PriceBar
{
    /// <summary>
    /// Gets the date of this price bar.
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Gets the opening price.
    /// </summary>
    public double Open { get; init; }

    /// <summary>
    /// Gets the highest price during the period.
    /// </summary>
    public double High { get; init; }

    /// <summary>
    /// Gets the lowest price during the period.
    /// </summary>
    public double Low { get; init; }

    /// <summary>
    /// Gets the closing price.
    /// </summary>
    public double Close { get; init; }

    /// <summary>
    /// Gets the trading volume.
    /// </summary>
    public long Volume { get; init; }

    /// <summary>
    /// Gets the adjusted closing price (adjusted for splits/dividends).
    /// </summary>
    public double? AdjustedClose { get; init; }

    /// <summary>
    /// Validates that OHLC data is consistent.
    /// </summary>
    public void Validate()
    {
        if (High < Low)
        {
            throw new InvalidOperationException($"High ({High}) cannot be less than Low ({Low})");
        }

        if (High < Open || High < Close)
        {
            throw new InvalidOperationException($"High ({High}) must be >= Open ({Open}) and Close ({Close})");
        }

        if (Low > Open || Low > Close)
        {
            throw new InvalidOperationException($"Low ({Low}) must be <= Open ({Open}) and Close ({Close})");
        }

        if (Volume < 0)
        {
            throw new InvalidOperationException($"Volume ({Volume}) cannot be negative");
        }
    }
}