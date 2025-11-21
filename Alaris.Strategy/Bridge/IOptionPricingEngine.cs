using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;

namespace Alaris.Strategy.Bridge;

/// <summary>
/// Interface for pricing options using advanced models including negative rates.
/// Implementations use the DoubleBoundaryEngine for accurate American option pricing.
/// </summary>
public interface IOptionPricingEngine
{
    /// <summary>
    /// Prices a single American option using the double boundary engine.
    /// Supports both positive and negative interest rates.
    /// </summary>
    /// <param name="parameters">Option pricing parameters.</param>
    /// <returns>Complete pricing information including Greeks.</returns>
    public Task<OptionPricing> PriceOption(OptionParameters parameters);

    /// <summary>
    /// Prices a calendar spread (long back month, short front month).
    /// </summary>
    /// <param name="parameters">Calendar spread parameters.</param>
    /// <returns>Complete spread pricing including net Greeks and risk metrics.</returns>
    public Task<CalendarSpreadPricing> PriceCalendarSpread(CalendarSpreadParameters parameters);

    /// <summary>
    /// Calculates implied volatility from market price using bisection method.
    /// </summary>
    /// <param name="marketPrice">Observed market price.</param>
    /// <param name="parameters">Option parameters (except IV).</param>
    /// <returns>Implied volatility.</returns>
    public Task<double> CalculateImpliedVolatility(double marketPrice, OptionParameters parameters);
}

/// <summary>
/// Parameters for pricing a calendar spread.
/// </summary>
public sealed class CalendarSpreadParameters
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
    /// Gets the front month (short) expiration date.
    /// </summary>
    public Date FrontExpiry { get; init; } = new();

    /// <summary>
    /// Gets the back month (long) expiration date.
    /// </summary>
    public Date BackExpiry { get; init; } = new();

    /// <summary>
    /// Gets the implied volatility to use for pricing.
    /// </summary>
    public double ImpliedVolatility { get; init; }

    /// <summary>
    /// Gets the risk-free interest rate (can be negative).
    /// </summary>
    public double RiskFreeRate { get; init; }

    /// <summary>
    /// Gets the continuous dividend yield (can be negative).
    /// </summary>
    public double DividendYield { get; init; }

    /// <summary>
    /// Gets the option type (Call or Put).
    /// </summary>
    public Option.Type OptionType { get; init; }

    /// <summary>
    /// Gets the valuation date.
    /// </summary>
    public Date ValuationDate { get; init; } = new();

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
        if (BackExpiry is not null && FrontExpiry is not null)
        {
            if (BackExpiry.serialNumber() <= FrontExpiry.serialNumber())
            {
                throw new ArgumentException("Back month must expire after front month");
            }
        }
    }
}