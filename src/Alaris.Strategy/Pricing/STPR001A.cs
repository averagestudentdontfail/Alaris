namespace Alaris.Strategy.Pricing;

/// <summary>
/// Contains pricing and risk metrics for a calendar spread position.
/// A calendar spread buys a longer-dated option and sells a shorter-dated option at the same strike.
/// </summary>
public sealed class STPR001APricing
{
    /// <summary>
    /// Gets or sets the pricing details for the front-month (short) option.
    /// </summary>
    public OptionPricing FrontOption { get; set; } = new();

    /// <summary>
    /// Gets or sets the pricing details for the back-month (long) option.
    /// </summary>
    public OptionPricing BackOption { get; set; } = new();

    /// <summary>
    /// Gets or sets the net cost of the calendar spread (debit paid).
    /// SpreadCost = BackOption.Price - FrontOption.Price
    /// </summary>
    public double SpreadCost { get; set; }

    /// <summary>
    /// Gets or sets the net delta of the spread.
    /// </summary>
    public double SpreadDelta { get; set; }

    /// <summary>
    /// Gets or sets the net gamma of the spread.
    /// </summary>
    public double SpreadGamma { get; set; }

    /// <summary>
    /// Gets or sets the net vega of the spread.
    /// Calendar spreads are long vega (benefit from IV increase).
    /// </summary>
    public double SpreadVega { get; set; }

    /// <summary>
    /// Gets or sets the net theta of the spread.
    /// Calendar spreads benefit from time decay of the front-month option.
    /// </summary>
    public double SpreadTheta { get; set; }

    /// <summary>
    /// Gets or sets the net rho of the spread.
    /// </summary>
    public double SpreadRho { get; set; }

    /// <summary>
    /// Gets or sets the maximum theoretical profit.
    /// Typically occurs when underlying is at strike at front-month expiration.
    /// </summary>
    public double MaxProfit { get; set; }

    /// <summary>
    /// Gets or sets the maximum loss (limited to the debit paid).
    /// </summary>
    public double MaxLoss { get; set; }

    /// <summary>
    /// Gets or sets the breakeven price at front-month expiration.
    /// </summary>
    public double BreakEven { get; set; }

    /// <summary>
    /// Gets the profit/loss ratio.
    /// </summary>
    public double ProfitLossRatio => MaxLoss > 0 ? MaxProfit / MaxLoss : 0;

    /// <summary>
    /// Gets whether the spread has positive expected value based on IV/RV ratio.
    /// </summary>
    public bool HasPositiveExpectedValue { get; set; }

    /// <summary>
    /// Validates the calendar spread pricing is internally consistent.
    /// </summary>
    public void Validate()
    {
        if (SpreadCost < 0)
        {
            throw new InvalidOperationException("Calendar spread should have positive cost (debit spread)");
        }

        if ((BackOption.Price <= 0) || (FrontOption.Price <= 0))
        {
            throw new InvalidOperationException("Option prices must be positive");
        }

        if (Math.Abs(SpreadCost - (BackOption.Price - FrontOption.Price)) > 0.01)
        {
            throw new InvalidOperationException("Spread cost inconsistent with option prices");
        }
    }
}

/// <summary>
/// Contains pricing and Greeks for a single option contract.
/// </summary>
public sealed class OptionPricing
{
    /// <summary>
    /// Gets or sets the theoretical option price.
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// Gets or sets the option delta (∂Price/∂Underlying).
    /// Measures sensitivity to underlying price changes.
    /// Range: [0, 1] for calls, [-1, 0] for puts.
    /// </summary>
    public double Delta { get; set; }

    /// <summary>
    /// Gets or sets the option gamma (∂Delta/∂Underlying).
    /// Measures the rate of change of delta.
    /// </summary>
    public double Gamma { get; set; }

    /// <summary>
    /// Gets or sets the option vega (∂Price/∂Volatility).
    /// Measures sensitivity to volatility changes.
    /// </summary>
    public double Vega { get; set; }

    /// <summary>
    /// Gets or sets the option theta (∂Price/∂Time).
    /// Measures time decay (typically negative).
    /// </summary>
    public double Theta { get; set; }

    /// <summary>
    /// Gets or sets the option rho (∂Price/∂Rate).
    /// Measures sensitivity to interest rate changes.
    /// </summary>
    public double Rho { get; set; }

    /// <summary>
    /// Gets or sets the implied volatility used in pricing.
    /// </summary>
    public double ImpliedVolatility { get; set; }

    /// <summary>
    /// Gets or sets the time to expiration in years.
    /// </summary>
    public double TimeToExpiry { get; set; }

    /// <summary>
    /// Gets or sets the moneyness (Underlying / Strike).
    /// </summary>
    public double Moneyness { get; set; }
}