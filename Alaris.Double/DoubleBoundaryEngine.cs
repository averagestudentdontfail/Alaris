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
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DoubleBoundaryEngine.
    /// </summary>
    /// <param name="process">The Black-Scholes-Merton process for the underlying.</param>
    /// <param name="scheme">Optional iteration scheme for numerical solver.</param>
    /// <param name="equation">Fixed point equation type (default: IntrinsicValue).</param>
    public DoubleBoundaryEngine(
        GeneralizedBlackScholesProcess process,
        QdFpIterationScheme? scheme = null,
        QdFpAmericanEngine.FixedPointEquation equation = QdFpAmericanEngine.FixedPointEquation.IntrinsicValue)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        
        if (scheme is null)
        {
            _engine = new QdFpAmericanEngine(_process, equation);
        }
        else
        {
            _engine = new QdFpAmericanEngine(_process, scheme, equation);
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

        option.setPricingEngine(_engine);

        var result = new OptionResult
        {
            Price = option.NPV(),
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
    /// <returns>Pricing results with elapsed time in milliseconds.</returns>
    public (OptionResult Result, long ElapsedMs) CalculateWithTiming(VanillaOption option)
    {
        var sw = Stopwatch.StartNew();
        var result = Calculate(option);
        sw.Stop();
        
        return (result, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Performs sensitivity analysis by calculating option values across a range of spot prices.
    /// </summary>
    /// <param name="option">The vanilla option to analyze.</param>
    /// <param name="spotMin">Minimum spot price.</param>
    /// <param name="spotMax">Maximum spot price.</param>
    /// <param name="steps">Number of steps in the range.</param>
    /// <returns>List of spot prices and corresponding option results.</returns>
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

        var results = new List<(double, OptionResult)>();
        var spotStep = (spotMax - spotMin) / (steps - 1);
        
        // Get the underlying quote from the process
        var underlyingQuote = _process.stateVariable() as SimpleQuote;
        if (underlyingQuote is null)
            throw new InvalidOperationException("Process must use SimpleQuote for spot price");

        var originalSpot = underlyingQuote.value();

        try
        {
            for (int i = 0; i < steps; i++)
            {
                var spot = spotMin + i * spotStep;
                underlyingQuote.setValue(spot);
                
                var result = Calculate(option);
                results.Add((spot, result));
            }
        }
        finally
        {
            // Restore original spot price
            underlyingQuote.setValue(originalSpot);
        }

        return results;
    }

    /// <summary>
    /// Disposes of unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _engine?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Contains the complete pricing results for an American option including all Greeks.
/// </summary>
public sealed class OptionResult
{
    /// <summary>
    /// Gets or sets the option price (Net Present Value).
    /// </summary>
    public double Price { get; set; }

    /// <summary>
    /// Gets or sets Delta: sensitivity to underlying price (∂V/∂S).
    /// </summary>
    public double Delta { get; set; }

    /// <summary>
    /// Gets or sets Gamma: rate of change of Delta (∂²V/∂S²).
    /// </summary>
    public double Gamma { get; set; }

    /// <summary>
    /// Gets or sets Vega: sensitivity to volatility (∂V/∂σ).
    /// </summary>
    public double Vega { get; set; }

    /// <summary>
    /// Gets or sets Theta: time decay (∂V/∂t).
    /// </summary>
    public double Theta { get; set; }

    /// <summary>
    /// Gets or sets Rho: sensitivity to interest rate (∂V/∂r).
    /// </summary>
    public double Rho { get; set; }

    /// <summary>
    /// Gets the intrinsic value for a call option.
    /// </summary>
    public double IntrinsicValueCall(double spot, double strike) => Math.Max(spot - strike, 0);

    /// <summary>
    /// Gets the intrinsic value for a put option.
    /// </summary>
    public double IntrinsicValuePut(double spot, double strike) => Math.Max(strike - spot, 0);

    /// <summary>
    /// Gets the time value of the option.
    /// </summary>
    public double TimeValue(double spot, double strike, bool isCall) =>
        isCall ? Price - IntrinsicValueCall(spot, strike) : Price - IntrinsicValuePut(spot, strike);
}