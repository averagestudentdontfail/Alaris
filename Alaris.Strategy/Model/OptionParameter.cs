using Alaris.Quantlib;

namespace Alaris.Strategy.Model;

/// <summary>
/// Parameters required for pricing a single option contract.
/// </summary>
public sealed class OptionParameters
{
    /// <summary>
    /// Gets or sets the current price of the underlying security.
    /// </summary>
    public double UnderlyingPrice { get; set; }

    /// <summary>
    /// Gets or sets the strike price of the option.
    /// </summary>
    public double Strike { get; set; }

    /// <summary>
    /// Gets or sets the expiration date.
    /// </summary>
    public Date Expiry { get; set; } = new();

    /// <summary>
    /// Gets or sets the implied volatility (annual).
    /// </summary>
    public double ImpliedVolatility { get; set; }

    /// <summary>
    /// Gets or sets the risk-free interest rate (annual).
    /// </summary>
    public double RiskFreeRate { get; set; }

    /// <summary>
    /// Gets or sets the continuous dividend yield (annual).
    /// </summary>
    public double DividendYield { get; set; }

    /// <summary>
    /// Gets or sets the option type (Call or Put).
    /// </summary>
    public Option.Type OptionType { get; set; }

    /// <summary>
    /// Gets or sets the valuation date (pricing date).
    /// </summary>
    public Date ValuationDate { get; set; } = new();

    /// <summary>
    /// Validates the parameters for option pricing.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public void Validate()
    {
        if (UnderlyingPrice <= 0)
            throw new ArgumentException("Underlying price must be positive.", nameof(UnderlyingPrice));

        if (Strike <= 0)
            throw new ArgumentException("Strike price must be positive.", nameof(Strike));

        if (ImpliedVolatility < 0)
            throw new ArgumentException("Implied volatility cannot be negative.", nameof(ImpliedVolatility));

        if (Expiry is null)
            throw new ArgumentException("Expiry date must be set.", nameof(Expiry));

        if (ValuationDate is null)
            throw new ArgumentException("Valuation date must be set.", nameof(ValuationDate));
    }
}