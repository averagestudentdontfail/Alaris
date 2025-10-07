namespace Alaris.Strategy.Model;

/// <summary>
/// Represents a complete option chain for an underlying security with multiple expiration dates.
/// </summary>
public sealed class OptionChain
{
    /// <summary>
    /// Gets or sets the list of option expiration dates and their associated contracts.
    /// </summary>
    public List<OptionExpiry> Expiries { get; set; } = new();

    /// <summary>
    /// Gets or sets the underlying security symbol.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current price of the underlying security.
    /// </summary>
    public double UnderlyingPrice { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this option chain was retrieved.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents all option contracts for a specific expiration date.
/// </summary>
public sealed class OptionExpiry
{
    /// <summary>
    /// Gets or sets the expiration date for these options.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the list of call option contracts.
    /// </summary>
    public List<OptionContract> Calls { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of put option contracts.
    /// </summary>
    public List<OptionContract> Puts { get; set; } = new();

    /// <summary>
    /// Gets the number of days until expiration from a given date.
    /// </summary>
    public int GetDaysToExpiry(DateTime fromDate) => (ExpiryDate.Date - fromDate.Date).Days;
}

/// <summary>
/// Represents a single option contract with market data.
/// </summary>
public sealed class OptionContract
{
    /// <summary>
    /// Gets or sets the strike price of the option.
    /// </summary>
    public double Strike { get; set; }

    /// <summary>
    /// Gets or sets the best bid price.
    /// </summary>
    public double Bid { get; set; }

    /// <summary>
    /// Gets or sets the best ask price.
    /// </summary>
    public double Ask { get; set; }

    /// <summary>
    /// Gets or sets the last traded price.
    /// </summary>
    public double LastPrice { get; set; }

    /// <summary>
    /// Gets or sets the Black-Scholes implied volatility.
    /// </summary>
    public double ImpliedVolatility { get; set; }

    /// <summary>
    /// Gets or sets the option delta.
    /// </summary>
    public double Delta { get; set; }

    /// <summary>
    /// Gets or sets the option gamma.
    /// </summary>
    public double Gamma { get; set; }

    /// <summary>
    /// Gets or sets the option vega.
    /// </summary>
    public double Vega { get; set; }

    /// <summary>
    /// Gets or sets the option theta.
    /// </summary>
    public double Theta { get; set; }

    /// <summary>
    /// Gets or sets the open interest (number of outstanding contracts).
    /// </summary>
    public int OpenInterest { get; set; }

    /// <summary>
    /// Gets or sets the trading volume for the day.
    /// </summary>
    public int Volume { get; set; }

    /// <summary>
    /// Gets the mid-point price between bid and ask.
    /// </summary>
    public double MidPrice => (Bid + Ask) / 2.0;

    /// <summary>
    /// Gets the bid-ask spread.
    /// </summary>
    public double BidAskSpread => Ask - Bid;

    /// <summary>
    /// Gets the proportional bid-ask spread.
    /// </summary>
    public double ProportionalSpread => MidPrice > 0 ? BidAskSpread / MidPrice : 0;
}