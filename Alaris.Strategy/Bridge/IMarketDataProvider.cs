using Alaris.Strategy.Model;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Interface for accessing market data including option chains and historical prices.
/// Implementations might use Yahoo Finance, IEX Cloud, or other data providers.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// Retrieves the complete option chain for a symbol on a specific date.
    /// </summary>
    /// <param name="symbol">The underlying security symbol.</param>
    /// <param name="date">The date for which to retrieve the option chain.</param>
    /// <returns>The option chain with all available expiration dates and strikes.</returns>
    OptionChain GetOptionChain(string symbol, DateTime date);

    /// <summary>
    /// Retrieves historical OHLC price data for volatility calculations.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <param name="days">Number of historical days to retrieve.</param>
    /// <returns>List of price bars ordered chronologically.</returns>
    List<PriceBar> GetHistoricalPrices(string symbol, int days);

    /// <summary>
    /// Gets the current market price of a security.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <returns>The current price.</returns>
    double GetCurrentPrice(string symbol);

    /// <summary>
    /// Gets upcoming earnings announcement dates.
    /// </summary>
    /// <param name="symbol">The security symbol.</param>
    /// <returns>List of earnings dates.</returns>
    Task<List<DateTime>> GetEarningsDates(string symbol);

    /// <summary>
    /// Checks if market data is available for a symbol.
    /// </summary>
    Task<bool> IsDataAvailable(string symbol);
}

/// <summary>
/// Represents a single bar of OHLCV price data.
/// </summary>
public sealed class PriceBar
{
    /// <summary>
    /// Gets or sets the date of this price bar.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the opening price.
    /// </summary>
    public double Open { get; set; }

    /// <summary>
    /// Gets or sets the highest price during the period.
    /// </summary>
    public double High { get; set; }

    /// <summary>
    /// Gets or sets the lowest price during the period.
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// Gets or sets the closing price.
    /// </summary>
    public double Close { get; set; }

    /// <summary>
    /// Gets or sets the trading volume.
    /// </summary>
    public long Volume { get; set; }

    /// <summary>
    /// Gets or sets the adjusted closing price (adjusted for splits/dividends).
    /// </summary>
    public double? AdjustedClose { get; set; }

    /// <summary>
    /// Validates that OHLC data is consistent.
    /// </summary>
    public void Validate()
    {
        if (High < Low)
            throw new InvalidOperationException($"High ({High}) cannot be less than Low ({Low})");

        if (High < Open || High < Close)
            throw new InvalidOperationException($"High ({High}) must be >= Open ({Open}) and Close ({Close})");

        if (Low > Open || Low > Close)
            throw new InvalidOperationException($"Low ({Low}) must be <= Open ({Open}) and Close ({Close})");

        if (Volume < 0)
            throw new InvalidOperationException($"Volume ({Volume}) cannot be negative");
    }
}