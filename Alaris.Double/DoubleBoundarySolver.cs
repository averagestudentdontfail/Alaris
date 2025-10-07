using System;
using System.Collections.Generic;

namespace Alaris.Double;

/// <summary>
/// Solves for the optimal exercise boundaries of American options using the double boundary method.
/// Implements the Ju-Zhong (1999) quadratic approximation with iterative refinement.
/// Supports negative interest rates and provides high accuracy for near-expiration options.
/// </summary>
public sealed class DoubleBoundarySolver : IDisposable
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly DoubleBoundaryApproximation _approximation;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DoubleBoundarySolver.
    /// </summary>
    /// <param name="process">The Black-Scholes-Merton stochastic process.</param>
    /// <param name="strike">The strike price of the option.</param>
    /// <param name="maturity">Time to maturity in years.</param>
    /// <param name="riskFreeRate">The risk-free interest rate (can be negative).</param>
    /// <param name="dividendYield">The continuous dividend yield.</param>
    /// <param name="volatility">The volatility of the underlying asset.</param>
    /// <exception cref="ArgumentNullException">Thrown when process is null.</exception>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public DoubleBoundarySolver(
        GeneralizedBlackScholesProcess process,
        double strike,
        double maturity,
        double riskFreeRate,
        double dividendYield,
        double volatility)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        
        ValidateParameters(strike, maturity, volatility);

        _approximation = new DoubleBoundaryApproximation(
            process, strike, maturity, riskFreeRate, dividendYield, volatility);
    }

    /// <summary>
    /// Solves for the optimal exercise boundaries at the current evaluation date.
    /// </summary>
    /// <param name="spot">Current spot price of the underlying asset.</param>
    /// <param name="isCall">True for call options, false for put options.</param>
    /// <returns>Boundary solution including upper and lower exercise boundaries.</returns>
    /// <exception cref="ArgumentException">Thrown when spot price is invalid.</exception>
    public BoundaryResult SolveBoundaries(double spot, bool isCall)
    {
        if (spot <= 0)
            throw new ArgumentException("Spot price must be positive", nameof(spot));

        return _approximation.Calculate(spot, isCall);
    }

    /// <summary>
    /// Solves for boundaries and calculates the option value at the current spot price.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>Tuple containing the option value and boundary result.</returns>
    public (double OptionValue, BoundaryResult Boundaries) SolveWithValue(
        double spot, 
        double strike, 
        bool isCall)
    {
        var boundaries = SolveBoundaries(spot, isCall);
        var value = CalculateOptionValue(spot, strike, isCall, boundaries);
        
        return (value, boundaries);
    }

    /// <summary>
    /// Calculates the option value given the exercise boundaries.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <param name="boundaries">The exercise boundaries.</param>
    /// <returns>The option value.</returns>
    public double CalculateOptionValue(
        double spot, 
        double strike, 
        bool isCall, 
        BoundaryResult boundaries)
    {
        if (isCall)
        {
            // For calls, check if spot is above the upper boundary (exercise immediately)
            if (spot >= boundaries.UpperBoundary)
                return Math.Max(spot - strike, 0);
            
            // Otherwise, use the approximation value
            return _approximation.ApproximateValue(spot, strike, true, boundaries);
        }
        else
        {
            // For puts, check if spot is below the lower boundary (exercise immediately)
            if (spot <= boundaries.LowerBoundary)
                return Math.Max(strike - spot, 0);
            
            return _approximation.ApproximateValue(spot, strike, false, boundaries);
        }
    }

    /// <summary>
    /// Performs a sensitivity analysis across a range of spot prices.
    /// </summary>
    /// <param name="spotMin">Minimum spot price.</param>
    /// <param name="spotMax">Maximum spot price.</param>
    /// <param name="steps">Number of steps in the analysis.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <returns>List of spot prices with corresponding boundaries and values.</returns>
    public List<SensitivityPoint> AnalyzeSensitivity(
        double spotMin,
        double spotMax,
        int steps,
        double strike,
        bool isCall)
    {
        if (spotMin >= spotMax)
            throw new ArgumentException("spotMin must be less than spotMax");
        
        if (steps < 2)
            throw new ArgumentException("steps must be at least 2", nameof(steps));

        var results = new List<SensitivityPoint>();
        var spotStep = (spotMax - spotMin) / (steps - 1);

        for (int i = 0; i < steps; i++)
        {
            var spot = spotMin + i * spotStep;
            var (value, boundaries) = SolveWithValue(spot, strike, isCall);
            
            results.Add(new SensitivityPoint
            {
                Spot = spot,
                OptionValue = value,
                UpperBoundary = boundaries.UpperBoundary,
                LowerBoundary = boundaries.LowerBoundary,
                CrossingTime = boundaries.CrossingTime
            });
        }

        return results;
    }

    /// <summary>
    /// Validates input parameters for solver initialization.
    /// </summary>
    private static void ValidateParameters(double strike, double maturity, double volatility)
    {
        if (strike <= 0)
            throw new ArgumentException("Strike must be positive", nameof(strike));
        
        if (maturity <= 0)
            throw new ArgumentException("Maturity must be positive", nameof(maturity));
        
        if (volatility < 0)
            throw new ArgumentException("Volatility cannot be negative", nameof(volatility));
        
        if (volatility > 5.0)
            throw new ArgumentException("Volatility appears unreasonably high (>500%)", nameof(volatility));
    }

    /// <summary>
    /// Disposes of unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _approximation?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Represents a point in sensitivity analysis with spot price, option value, and boundaries.
/// </summary>
public sealed class SensitivityPoint
{
    /// <summary>
    /// Gets or sets the spot price.
    /// </summary>
    public double Spot { get; set; }

    /// <summary>
    /// Gets or sets the option value at this spot price.
    /// </summary>
    public double OptionValue { get; set; }

    /// <summary>
    /// Gets or sets the upper exercise boundary (for calls).
    /// </summary>
    public double UpperBoundary { get; set; }

    /// <summary>
    /// Gets or sets the lower exercise boundary (for puts).
    /// </summary>
    public double LowerBoundary { get; set; }

    /// <summary>
    /// Gets or sets the estimated time until boundary crossing.
    /// </summary>
    public double CrossingTime { get; set; }

    /// <summary>
    /// Gets the intrinsic value for a call option.
    /// </summary>
    public double IntrinsicValueCall(double strike) => Math.Max(Spot - strike, 0);

    /// <summary>
    /// Gets the intrinsic value for a put option.
    /// </summary>
    public double IntrinsicValuePut(double strike) => Math.Max(strike - Spot, 0);

    /// <summary>
    /// Gets the time value (option value minus intrinsic value).
    /// </summary>
    public double TimeValue(double strike, bool isCall) =>
        isCall ? OptionValue - IntrinsicValueCall(strike) : OptionValue - IntrinsicValuePut(strike);
}