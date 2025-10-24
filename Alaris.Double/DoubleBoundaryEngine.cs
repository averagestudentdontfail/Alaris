using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Alaris.Double;

/// <summary>
/// Advanced American option pricing engine using the double boundary method.
/// Supports negative interest rates and provides accurate pricing with Greeks.
/// Based on the Ju-Zhong (1999) quadratic approximation method.
/// </summary>
public sealed class DoubleBoundaryEngine : IDisposable
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly QdFpAmericanEngine _engine;
    private readonly SimpleQuote? _underlyingQuote;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DoubleBoundaryEngine.
    /// </summary>
    /// <param name="process">The Black-Scholes-Merton process for the underlying.</param>
    /// <param name="scheme">Optional iteration scheme for numerical solver.</param>
    public DoubleBoundaryEngine(
        GeneralizedBlackScholesProcess process,
        QdFpIterationScheme? scheme = null)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        
        // Extract the underlying quote for sensitivity analysis
        var stateVariable = _process.stateVariable();
        var quote = stateVariable.currentLink();
        _underlyingQuote = quote as SimpleQuote;
        
        if (scheme is null)
        {
            _engine = new QdFpAmericanEngine(_process);
        }
        else
        {
            _engine = new QdFpAmericanEngine(_process, scheme);
        }
    }

    /// <summary>
    /// Implicit conversion to PricingEngine for seamless integration with QuantLib VanillaOption.
    /// </summary>
    /// <param name="engine">The DoubleBoundaryEngine to convert.</param>
    public static implicit operator PricingEngine(DoubleBoundaryEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine._engine;
    }

    /// <summary>
    /// Gets the underlying PricingEngine for explicit use with VanillaOption.
    /// </summary>
    /// <returns>The underlying QdFpAmericanEngine.</returns>
    public PricingEngine GetPricingEngine() => _engine;

    /// <summary>
    /// Calculates the option price and Greeks for an American option.
    /// </summary>
    /// <param name="option">The vanilla option to price.</param>
    /// <returns>Complete option pricing results including all Greeks.</returns>
    public OptionResult Calculate(VanillaOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        // Set the pricing engine
        option.setPricingEngine(_engine);

        // Trigger calculation by accessing NPV first
        // This ensures QuantLib's lazy evaluation completes before accessing Greeks
        double price = option.NPV();

        // Now Greeks should be available
        var result = new OptionResult
        {
            Price = price,
            Delta = option.delta(),
            Gamma = option.gamma(),
            Vega = option.vega(),
            Theta = option.theta(),
            Rho = option.rho()
        };

        return result;
    }

    /// <summary>
    /// Calculates option price and Greeks with performance timing.
    /// </summary>
    /// <param name="option">The vanilla option to price.</param>
    /// <returns>Tuple containing results and elapsed time in milliseconds.</returns>
    public (OptionResult Result, long ElapsedMilliseconds) CalculateWithTiming(VanillaOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        var stopwatch = Stopwatch.StartNew();
        var result = Calculate(option);
        stopwatch.Stop();

        return (result, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Performs sensitivity analysis by varying the underlying spot price.
    /// </summary>
    /// <param name="option">The vanilla option to analyze.</param>
    /// <param name="spotMin">Minimum spot price for analysis.</param>
    /// <param name="spotMax">Maximum spot price for analysis.</param>
    /// <param name="steps">Number of steps in the spot price range.</param>
    /// <returns>List of tuples containing spot prices and corresponding option results.</returns>
    public List<(double Spot, OptionResult Result)> SensitivityAnalysis(
        VanillaOption option,
        double spotMin,
        double spotMax,
        int steps)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (spotMin >= spotMax)
            throw new ArgumentException("spotMin must be less than spotMax");

        if (steps < 2)
            throw new ArgumentException("steps must be at least 2", nameof(steps));

        if (_underlyingQuote is null)
            throw new InvalidOperationException("Cannot perform sensitivity analysis: underlying quote not available");

        var results = new List<(double, OptionResult)>(steps);
        double spotStep = (spotMax - spotMin) / (steps - 1);
        double originalSpot = _underlyingQuote.value();

        try
        {
            for (int i = 0; i < steps; i++)
            {
                double spot = spotMin + i * spotStep;
                
                // Update the underlying spot price
                _underlyingQuote.setValue(spot);

                // Calculate option value at this spot price
                var result = Calculate(option);
                
                results.Add((spot, result));
            }
        }
        finally
        {
            // Restore original spot price
            _underlyingQuote.setValue(originalSpot);
        }

        return results;
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _engine?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Complete option pricing results including price and all Greeks.
/// </summary>
public sealed class OptionResult
{
    /// <summary>Gets or sets the option price (NPV).</summary>
    public double Price { get; init; }

    /// <summary>Gets or sets delta: rate of change of option value with respect to underlying price.</summary>
    public double Delta { get; init; }

    /// <summary>Gets or sets gamma: rate of change of delta with respect to underlying price.</summary>
    public double Gamma { get; init; }

    /// <summary>Gets or sets vega: sensitivity to volatility changes.</summary>
    public double Vega { get; init; }

    /// <summary>Gets or sets theta: rate of time decay.</summary>
    public double Theta { get; init; }

    /// <summary>Gets or sets rho: sensitivity to interest rate changes.</summary>
    public double Rho { get; init; }
}