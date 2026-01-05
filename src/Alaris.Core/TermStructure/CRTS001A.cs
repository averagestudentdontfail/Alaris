// CRTS001A.cs - Yield curve interface and flat forward implementation
// Component ID: CRTS001A
//
// Replaces: QuantLib.YieldTermStructure, QuantLib.FlatForward
//
// Mathematical Specification:
// - DiscountFactor(t) = exp(-r * t) for flat forward rate r
// - ZeroRate(t) = r for flat curve
// - ForwardRate(t) = r for flat curve
//
// References:
// - Hull, J.C. "Options, Futures, and Other Derivatives" Chapter 4
// - QuantLib yieldtermstructure.cpp
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)

using Alaris.Core.Time;
using System.Diagnostics.CodeAnalysis;

namespace Alaris.Core.TermStructure;

/// <summary>
/// Interface for yield term structures (discount curves).
/// Replaces QuantLib YieldTermStructure.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Domain-specific financial terminology")]
public interface IYieldCurve
{
    /// <summary>
    /// Gets the reference date (valuation date) for this curve.
    /// </summary>
    public CRTM005A ReferenceDate { get; }

    /// <summary>
    /// Gets the day count convention used by this curve.
    /// </summary>
    public IDayCounter DayCounter { get; }

    /// <summary>
    /// Calculates the discount factor for a given time.
    /// </summary>
    /// <param name="time">Time in years from reference date.</param>
    /// <returns>Discount factor P(0,T).</returns>
    public double DiscountFactor(double time);

    /// <summary>
    /// Calculates the continuously compounded zero rate for a given time.
    /// </summary>
    /// <param name="time">Time in years from reference date.</param>
    /// <returns>Zero rate r such that P(0,T) = exp(-r*T).</returns>
    public double ZeroRate(double time);

    /// <summary>
    /// Calculates the instantaneous forward rate at a given time.
    /// </summary>
    /// <param name="time">Time in years from reference date.</param>
    /// <returns>Instantaneous forward rate f(T).</returns>
    public double ForwardRate(double time);

    /// <summary>
    /// Calculates the discount factor for a given date.
    /// </summary>
    /// <param name="date">Target date.</param>
    /// <returns>Discount factor P(0,T) where T = yearFraction(refDate, date).</returns>
    public double DiscountFactor(CRTM005A date);
}

/// <summary>
/// Flat forward yield curve with constant rate.
/// Replaces QuantLib FlatForward.
/// </summary>
/// <remarks>
/// For a flat forward curve with rate r:
/// - All forward rates are r
/// - All zero rates are r
/// - Discount factor at time T is exp(-r*T)
/// </remarks>
public sealed class CRTS001AFlatForward : IYieldCurve
{
    private readonly double _rate;

    /// <summary>
    /// Initialises a flat forward yield curve.
    /// </summary>
    /// <param name="referenceDate">The reference/valuation date.</param>
    /// <param name="rate">The flat forward rate (continuously compounded).</param>
    /// <param name="dayCounter">Day count convention for time calculations.</param>
    public CRTS001AFlatForward(CRTM005A referenceDate, double rate, IDayCounter? dayCounter = null)
    {
        ReferenceDate = referenceDate;
        _rate = rate;
        DayCounter = dayCounter ?? DayCounters.Actual365Fixed;
    }

    /// <inheritdoc/>
    public CRTM005A ReferenceDate { get; }

    /// <inheritdoc/>
    public IDayCounter DayCounter { get; }

    /// <inheritdoc/>
    public double DiscountFactor(double time)
    {
        if (time <= 0.0)
        {
            return 1.0;
        }
        return System.Math.Exp(-_rate * time);
    }

    /// <inheritdoc/>
    public double ZeroRate(double time)
    {
        return _rate;
    }

    /// <inheritdoc/>
    public double ForwardRate(double time)
    {
        return _rate;
    }

    /// <inheritdoc/>
    public double DiscountFactor(CRTM005A date)
    {
        double time = DayCounter.YearFraction(ReferenceDate, date);
        return DiscountFactor(time);
    }

    /// <summary>
    /// Gets the flat rate of this curve.
    /// </summary>
    public double Rate => _rate;
}
