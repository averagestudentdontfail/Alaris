// STDT003A.cs - Option pricing parameters model
// Component ID: STDT003A
//
// This model uses native Alaris types instead of QuantLib types.
// Migration: QuantLib.Date → CRTM005A, QuantLib.Option.Type → OptionType

using Alaris.Core.Options;
using Alaris.Core.Time;

namespace Alaris.Strategy.Model;

/// <summary>
/// Parameters required for pricing a single option contract.
/// </summary>
public sealed class STDT003A
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
    public CRTM005A Expiry { get; set; }

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
    public OptionType OptionType { get; set; }

    /// <summary>
    /// Gets or sets the valuation date (pricing date).
    /// </summary>
    public CRTM005A ValuationDate { get; set; }

    /// <summary>
    /// Calculates time to expiry in years.
    /// </summary>
    /// <returns>Time to expiry in years using Actual/365 fixed convention.</returns>
    public double TimeToExpiry()
    {
        return DayCounters.Actual365Fixed.YearFraction(ValuationDate, Expiry);
    }

    /// <summary>
    /// Validates the parameters for option pricing.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public void Validate()
    {
        if (UnderlyingPrice <= 0)
        {
            throw new ArgumentException("Underlying price must be positive.", nameof(UnderlyingPrice));
        }

        if (Strike <= 0)
        {
            throw new ArgumentException("Strike price must be positive.", nameof(Strike));
        }

        if (ImpliedVolatility < 0)
        {
            throw new ArgumentException("Implied volatility cannot be negative.", nameof(ImpliedVolatility));
        }

        if (Expiry.SerialNumber == 0)
        {
            throw new ArgumentException("Expiry date must be set.", nameof(Expiry));
        }

        if (ValuationDate.SerialNumber == 0)
        {
            throw new ArgumentException("Valuation date must be set.", nameof(ValuationDate));
        }
    }
}