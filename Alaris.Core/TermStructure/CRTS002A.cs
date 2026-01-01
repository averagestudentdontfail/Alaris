// CRTS002A.cs - Volatility surface interface and flat vol implementation
// Component ID: CRTS002A
//
// Replaces: QuantLib.BlackVolTermStructure, QuantLib.BlackConstantVol
//
// Mathematical Specification:
// - BlackVol(T, K) = σ for flat volatility σ
// - Variance(T, K) = σ² * T
//
// References:
// - Hull, J.C. "Options, Futures, and Other Derivatives" Chapter 20
// - QuantLib blackvoltermstructure.cpp
// - Alaris.Governance/Coding.md Rule 8 (Limited Scope)

using Alaris.Core.Time;
using System.Diagnostics.CodeAnalysis;

namespace Alaris.Core.TermStructure;

/// <summary>
/// Interface for Black volatility term structures (volatility surfaces).
/// Replaces QuantLib BlackVolTermStructure.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Domain-specific financial terminology")]
public interface IVolatilitySurface
{
    /// <summary>
    /// Gets the reference date (valuation date) for this surface.
    /// </summary>
    public CRTM005A ReferenceDate { get; }

    /// <summary>
    /// Gets the day count convention used by this surface.
    /// </summary>
    public IDayCounter DayCounter { get; }

    /// <summary>
    /// Gets the Black volatility for a given time and strike.
    /// </summary>
    /// <param name="time">Time to expiry in years.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>Black volatility σ.</returns>
    public double BlackVol(double time, double strike);

    /// <summary>
    /// Gets the Black variance for a given time and strike.
    /// </summary>
    /// <param name="time">Time to expiry in years.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>Black variance σ²T.</returns>
    public double BlackVariance(double time, double strike);

    /// <summary>
    /// Gets the Black volatility for a given date and strike.
    /// </summary>
    /// <param name="date">Expiry date.</param>
    /// <param name="strike">Strike price.</param>
    /// <returns>Black volatility σ.</returns>
    public double BlackVol(CRTM005A date, double strike);
}

/// <summary>
/// Flat Black volatility surface with constant volatility.
/// Replaces QuantLib BlackConstantVol.
/// </summary>
/// <remarks>
/// For a flat volatility surface:
/// - σ(T, K) = σ for all T and K
/// - Variance = σ² * T
/// </remarks>
public sealed class CRTS002AFlatVol : IVolatilitySurface
{
    private readonly double _volatility;

    /// <summary>
    /// Initialises a flat Black volatility surface.
    /// </summary>
    /// <param name="referenceDate">The reference/valuation date.</param>
    /// <param name="volatility">The flat volatility (annualised).</param>
    /// <param name="dayCounter">Day count convention for time calculations.</param>
    public CRTS002AFlatVol(CRTM005A referenceDate, double volatility, IDayCounter? dayCounter = null)
    {
        if (volatility < 0)
        {
            throw new ArgumentException("Volatility cannot be negative", nameof(volatility));
        }

        ReferenceDate = referenceDate;
        _volatility = volatility;
        DayCounter = dayCounter ?? DayCounters.Actual365Fixed;
    }

    /// <inheritdoc/>
    public CRTM005A ReferenceDate { get; }

    /// <inheritdoc/>
    public IDayCounter DayCounter { get; }

    /// <inheritdoc/>
    public double BlackVol(double time, double strike)
    {
        // Strike-independent for flat vol
        _ = strike;
        return _volatility;
    }

    /// <inheritdoc/>
    public double BlackVariance(double time, double strike)
    {
        double vol = BlackVol(time, strike);
        return vol * vol * System.Math.Max(time, 0.0);
    }

    /// <inheritdoc/>
    public double BlackVol(CRTM005A date, double strike)
    {
        double time = DayCounter.YearFraction(ReferenceDate, date);
        return BlackVol(time, strike);
    }

    /// <summary>
    /// Gets the flat volatility of this surface.
    /// </summary>
    public double Volatility => _volatility;
}
