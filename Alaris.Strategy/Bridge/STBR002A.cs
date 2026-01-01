// STBR002A.cs - Option pricing bridge interface
// Component ID: STBR002A
//
// Migrated from QuantLib types to native Alaris types.
// Date → CRTM005A, Option.Type → OptionType

using Alaris.Core.Options;
using Alaris.Core.Time;
using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Interface for pricing options using advanced models including negative rates.
/// Implementations use the native Alaris pricing engine for accurate American option pricing.
/// </summary>
public interface STBR002A
{
    /// <summary>
    /// Prices a single American option.
    /// Supports both positive and negative interest rates.
    /// </summary>
    /// <param name="parameters">Option pricing parameters.</param>
    /// <returns>Complete pricing information including Greeks.</returns>
    public Task<OptionPricing> PriceOption(STDT003A parameters);

    /// <summary>
    /// Prices a calendar spread (long back month, short front month).
    /// </summary>
    /// <param name="parameters">Calendar spread parameters.</param>
    /// <returns>Complete spread pricing including net Greeks and risk metrics.</returns>
    public Task<STPR001APricing> PriceSTPR001A(STPR001AParameters parameters);

    /// <summary>
    /// Calculates implied volatility from market price using deterministic methods.
    /// </summary>
    /// <param name="marketPrice">Observed market price.</param>
    /// <param name="parameters">Option parameters (except IV).</param>
    /// <returns>Implied volatility.</returns>
    public Task<double> CalculateImpliedVolatility(double marketPrice, STDT003A parameters);
}

/// <summary>
/// Parameters for pricing a calendar spread.
/// </summary>
public sealed class STPR001AParameters
{
    /// <summary>
    /// Gets the current price of the underlying security.
    /// </summary>
    public double UnderlyingPrice { get; init; }

    /// <summary>
    /// Gets the strike price (same for both legs).
    /// </summary>
    public double Strike { get; init; }

    /// <summary>
    /// Gets or sets the front month (short) expiration date.
    /// </summary>
    public CRTM005A FrontExpiry { get; set; }

    /// <summary>
    /// Gets or sets the back month (long) expiration date.
    /// </summary>
    public CRTM005A BackExpiry { get; set; }

    /// <summary>
    /// Gets or sets the implied volatility to use for pricing.
    /// </summary>
    public double ImpliedVolatility { get; set; }

    /// <summary>
    /// Gets or sets the risk-free interest rate (can be negative).
    /// </summary>
    public double RiskFreeRate { get; set; }

    /// <summary>
    /// Gets or sets the continuous dividend yield (can be negative).
    /// </summary>
    public double DividendYield { get; set; }

    /// <summary>
    /// Gets the option type (Call or Put).
    /// </summary>
    public OptionType OptionType { get; init; }

    /// <summary>
    /// Gets the valuation date.
    /// </summary>
    public CRTM005A ValuationDate { get; init; }

    /// <summary>
    /// Calculates time to front expiry in years.
    /// </summary>
    public double TimeToFrontExpiry()
    {
        return DayCounters.Actual365Fixed.YearFraction(ValuationDate, FrontExpiry);
    }

    /// <summary>
    /// Calculates time to back expiry in years.
    /// </summary>
    public double TimeToBackExpiry()
    {
        return DayCounters.Actual365Fixed.YearFraction(ValuationDate, BackExpiry);
    }

    /// <summary>
    /// Validates the calendar spread parameters.
    /// </summary>
    public void Validate()
    {
        if (UnderlyingPrice <= 0)
        {
            throw new ArgumentException("Underlying price must be positive", nameof(UnderlyingPrice));
        }

        if (Strike <= 0)
        {
            throw new ArgumentException("Strike must be positive", nameof(Strike));
        }

        if (ImpliedVolatility < 0)
        {
            throw new ArgumentException("Implied volatility cannot be negative", nameof(ImpliedVolatility));
        }

        // Back month must expire after front month
        if (BackExpiry.SerialNumber <= FrontExpiry.SerialNumber)
        {
            throw new ArgumentException("Back month must expire after front month");
        }
    }
}