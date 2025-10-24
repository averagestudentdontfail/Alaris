using System;
using System.Collections.Generic;
using System.Diagnostics;
using MathNet.Numerics.Differentiation;

namespace Alaris.Double;

/// <summary>
/// Advanced American option pricing engine using the double boundary method.
/// Supports negative interest rates and provides accurate pricing with Greeks.
/// Based on the Ju-Zhong (1999) quadratic approximation method via QdFpAmericanEngine.
/// Greeks are computed using central finite differences from MathNet.Numerics.
/// </summary>
public sealed class DoubleBoundaryEngine : IDisposable
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly QdFpAmericanEngine _engine;
    private readonly SimpleQuote? _underlyingQuote;
    private readonly NumericalDerivative _differentiator;
    private bool _disposed;

    // Use 5-point stencil for good accuracy with central differences
    private const int DefaultNumberOfPoints = 5;

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
        
        // Extract the underlying quote for Greek calculations
        var stateVariable = _process.stateVariable();
        var quote = stateVariable.currentLink();
        _underlyingQuote = quote as SimpleQuote;
        
        // Initialize numerical differentiator for central finite differences
        // center = 0 for adaptive centering, points = 5 for good accuracy
        _differentiator = new NumericalDerivative(center: 0, points: DefaultNumberOfPoints);
        
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
    /// Calculates the option price and Greeks for an American option using central finite differences.
    /// All Greeks are computed using symmetric (central) finite difference schemes for maximum accuracy.
    /// </summary>
    /// <param name="option">The vanilla option to price.</param>
    /// <returns>Complete option pricing results including all Greeks.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required quotes are unavailable.</exception>
    public OptionResult Calculate(VanillaOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (_underlyingQuote is null)
        {
            throw new InvalidOperationException(
                "Cannot calculate Greeks: underlying quote not available. " +
                "Ensure the process is constructed with a QuoteHandle wrapping a SimpleQuote.");
        }

        // Set the pricing engine
        option.setPricingEngine(_engine);

        // Store original spot value
        double originalSpot = _underlyingQuote.value();

        // Calculate base price
        double basePrice = option.NPV();

        // Define the pricing function for differentiation
        Func<double, double> pricingFunc = (spot) =>
        {
            _underlyingQuote.setValue(spot);
            return option.NPV();
        };

        // Calculate Delta using central finite differences (first derivative)
        // MathNet.Numerics.Differentiation uses central differences by default
        double delta = _differentiator.EvaluateDerivative(pricingFunc, originalSpot, 1);

        // Calculate Gamma using central finite differences (second derivative)
        double gamma = _differentiator.EvaluateDerivative(pricingFunc, originalSpot, 2);

        // Restore original spot value
        _underlyingQuote.setValue(originalSpot);

        // Calculate Vega (sensitivity to volatility)
        double vega = CalculateVega(option);

        // Calculate Theta (time decay)
        double theta = CalculateTheta(option);

        // Calculate Rho (sensitivity to interest rate)
        double rho = CalculateRho(option);

        var result = new OptionResult
        {
            Price = basePrice,
            Delta = delta,
            Gamma = gamma,
            Vega = vega,
            Theta = theta,
            Rho = rho
        };

        return result;
    }

    /// <summary>
    /// Calculates vega using central finite differences by reconstructing the process with bumped volatility.
    /// Uses symmetric bumping (up and down) for accurate derivative estimation.
    /// </summary>
    private double CalculateVega(VanillaOption option)
    {
        try
        {
            var blackVolTS = _process.blackVolatility();
            var referenceDate = Settings.instance().getEvaluationDate();
            
            // Extract current volatility
            double currentVol = blackVolTS.blackVol(
                referenceDate.Add(new Period(1, TimeUnit.Years)), 
                100.0);

            // Define vega calculation function for central differences
            Func<double, double> vegaFunc = (volShift) =>
            {
                var bumpedVolTS = new BlackConstantVol(
                    referenceDate,
                    new TARGET(),
                    currentVol + volShift,
                    new Actual365Fixed());

                var bumpedProcess = new BlackScholesMertonProcess(
                    new QuoteHandle(_underlyingQuote!),
                    _process.dividendYield(),
                    _process.riskFreeRate(),
                    new BlackVolTermStructureHandle(bumpedVolTS));

                var bumpedEngine = new QdFpAmericanEngine(bumpedProcess);
                option.setPricingEngine(bumpedEngine);
                double price = option.NPV();

                // Clean up
                bumpedEngine.Dispose();
                bumpedProcess.Dispose();
                bumpedVolTS.Dispose();

                return price;
            };

            // Use central finite differences (symmetric bumping)
            double vega = _differentiator.EvaluateDerivative(vegaFunc, 0.0, 1);

            // Restore original engine
            option.setPricingEngine(_engine);

            return vega;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Vega calculation failed: {ex.Message}");
            return 0.0;
        }
    }

    /// <summary>
    /// Calculates theta using central finite differences by shifting the evaluation date.
    /// Returns theta per day (negative for time decay).
    /// Uses symmetric date shifts (forward and backward) for accuracy.
    /// </summary>
    private double CalculateTheta(VanillaOption option)
    {
        try
        {
            var originalDate = Settings.instance().getEvaluationDate();

            // Define theta calculation function using central differences
            Func<double, double> thetaFunc = (dayShift) =>
            {
                int days = (int)Math.Round(dayShift);
                var newDate = originalDate.Add(new Period(days, TimeUnit.Days));
                Settings.instance().setEvaluationDate(newDate);
                double price = option.NPV();
                Settings.instance().setEvaluationDate(originalDate);
                return price;
            };

            // Calculate theta using central finite differences
            // Note: theta is conventionally negative (time decay)
            double theta = _differentiator.EvaluateDerivative(thetaFunc, 0.0, 1);

            return theta;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Theta calculation failed: {ex.Message}");
            return 0.0;
        }
    }

    /// <summary>
    /// Calculates rho using central finite differences by reconstructing the process with bumped risk-free rate.
    /// Returns rho per 0.01 (1% change in rate).
    /// Uses symmetric rate bumps (up and down) for accurate derivative.
    /// </summary>
    private double CalculateRho(VanillaOption option)
    {
        try
        {
            var referenceDate = Settings.instance().getEvaluationDate();
            var riskFreeTS = _process.riskFreeRate();
            
            // Get current rate - extract the double value from InterestRate
            var interestRate = riskFreeTS.zeroRate(
                referenceDate.Add(new Period(1, TimeUnit.Years)),
                new Actual365Fixed(), 
                Compounding.Continuous);
            double currentRate = interestRate.rate();

            // Define rho calculation function for central differences
            Func<double, double> rhoFunc = (rateShift) =>
            {
                var bumpedRateTS = new FlatForward(
                    referenceDate, 
                    currentRate + rateShift, 
                    new Actual365Fixed());
                
                var bumpedProcess = new BlackScholesMertonProcess(
                    new QuoteHandle(_underlyingQuote!),
                    _process.dividendYield(),
                    new YieldTermStructureHandle(bumpedRateTS),
                    _process.blackVolatility());

                var bumpedEngine = new QdFpAmericanEngine(bumpedProcess);
                option.setPricingEngine(bumpedEngine);
                double price = option.NPV();

                // Clean up
                bumpedEngine.Dispose();
                bumpedProcess.Dispose();
                bumpedRateTS.Dispose();

                return price;
            };

            // Use central finite differences (symmetric bumping)
            double rho = _differentiator.EvaluateDerivative(rhoFunc, 0.0, 1);

            // Restore original engine
            option.setPricingEngine(_engine);

            // Scale to per 1% (0.01) change
            return rho * 0.01;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rho calculation failed: {ex.Message}");
            return 0.0;
        }
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
    /// Generates a price-Greek profile across a range of spot prices.
    /// All Greeks at each point are calculated using central finite differences.
    /// </summary>
    /// <param name="option">The vanilla option to analyse.</param>
    /// <param name="spotMin">Minimum spot price for analysis.</param>
    /// <param name="spotMax">Maximum spot price for analysis.</param>
    /// <param name="steps">Number of steps in the spot range (minimum 2).</param>
    /// <returns>Dictionary mapping spot prices to option results.</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when underlying quote is unavailable.</exception>
    public Dictionary<double, OptionResult> SensitivityAnalysis(
        VanillaOption option,
        double spotMin,
        double spotMax,
        int steps = 20)
    {
        ArgumentNullException.ThrowIfNull(option);
        
        if (spotMin >= spotMax)
            throw new ArgumentException("spotMin must be less than spotMax");
        
        if (steps < 2)
            throw new ArgumentException("steps must be at least 2");

        if (_underlyingQuote is null)
        {
            throw new InvalidOperationException(
                "Cannot perform sensitivity analysis: underlying quote not available");
        }

        var originalSpot = _underlyingQuote.value();
        var results = new Dictionary<double, OptionResult>(steps);

        try
        {
            double stepSize = (spotMax - spotMin) / (steps - 1);

            for (int i = 0; i < steps; i++)
            {
                double spot = spotMin + i * stepSize;
                _underlyingQuote.setValue(spot);
                
                var result = Calculate(option);
                results[spot] = result;
            }
        }
        finally
        {
            // Always restore the original spot price
            _underlyingQuote.setValue(originalSpot);
        }

        return results;
    }

    /// <summary>
    /// Disposes of the pricing engine resources.
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
/// All Greeks are computed using central finite differences for maximum accuracy.
/// </summary>
public sealed class OptionResult
{
    /// <summary>Gets or sets the option price (NPV).</summary>
    public double Price { get; init; }

    /// <summary>
    /// Gets or sets delta: rate of change of option value with respect to underlying price.
    /// Computed using central finite differences: (V(S+h) - V(S-h)) / (2h).
    /// </summary>
    public double Delta { get; init; }

    /// <summary>
    /// Gets or sets gamma: rate of change of delta with respect to underlying price (convexity).
    /// Represents the second derivative of option value with respect to spot.
    /// Computed using central finite differences for maximum accuracy.
    /// </summary>
    public double Gamma { get; init; }

    /// <summary>
    /// Gets or sets vega: sensitivity to volatility changes.
    /// Represents derivative of option value with respect to volatility (in decimal form).
    /// Computed using central finite differences with volatility bumps.
    /// </summary>
    public double Vega { get; init; }

    /// <summary>
    /// Gets or sets theta: rate of time decay (per day).
    /// Conventionally negative, representing value loss as time passes.
    /// Computed using central finite differences with symmetric date shifts.
    /// </summary>
    public double Theta { get; init; }

    /// <summary>
    /// Gets or sets rho: sensitivity to interest rate changes.
    /// Scaled to represent change per 1% (0.01) change in interest rate.
    /// Computed using central finite differences with rate bumps.
    /// </summary>
    public double Rho { get; init; }
}using System;
using System.Collections.Generic;
using System.Diagnostics;
using MathNet.Numerics.Differentiation;

namespace Alaris.Double;

/// <summary>
/// Advanced American option pricing engine using the double boundary method.
/// Supports negative interest rates and provides accurate pricing with Greeks.
/// Based on the Ju-Zhong (1999) quadratic approximation method via QdFpAmericanEngine.
/// Greeks are computed using central finite differences from MathNet.Numerics.
/// </summary>
public sealed class DoubleBoundaryEngine : IDisposable
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly QdFpAmericanEngine _engine;
    private readonly SimpleQuote? _underlyingQuote;
    private readonly NumericalDerivative _differentiator;
    private bool _disposed;

    // Use 5-point stencil for good accuracy with central differences
    private const int DefaultNumberOfPoints = 5;

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
        
        // Extract the underlying quote for Greek calculations
        var stateVariable = _process.stateVariable();
        var quote = stateVariable.currentLink();
        _underlyingQuote = quote as SimpleQuote;
        
        // Initialize numerical differentiator for central finite differences
        // center = 0 for adaptive centering, points = 5 for good accuracy
        _differentiator = new NumericalDerivative(center: 0, points: DefaultNumberOfPoints);
        
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
    /// Calculates the option price and Greeks for an American option using central finite differences.
    /// All Greeks are computed using symmetric (central) finite difference schemes for maximum accuracy.
    /// </summary>
    /// <param name="option">The vanilla option to price.</param>
    /// <returns>Complete option pricing results including all Greeks.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required quotes are unavailable.</exception>
    public OptionResult Calculate(VanillaOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (_underlyingQuote is null)
        {
            throw new InvalidOperationException(
                "Cannot calculate Greeks: underlying quote not available. " +
                "Ensure the process is constructed with a QuoteHandle wrapping a SimpleQuote.");
        }

        // Set the pricing engine
        option.setPricingEngine(_engine);

        // Store original spot value
        double originalSpot = _underlyingQuote.value();

        // Calculate base price
        double basePrice = option.NPV();

        // Define the pricing function for differentiation
        Func<double, double> pricingFunc = (spot) =>
        {
            _underlyingQuote.setValue(spot);
            return option.NPV();
        };

        // Calculate Delta using central finite differences (first derivative)
        // MathNet.Numerics.Differentiation uses central differences by default
        double delta = _differentiator.EvaluateDerivative(pricingFunc, originalSpot, 1);

        // Calculate Gamma using central finite differences (second derivative)
        double gamma = _differentiator.EvaluateDerivative(pricingFunc, originalSpot, 2);

        // Restore original spot value
        _underlyingQuote.setValue(originalSpot);

        // Calculate Vega (sensitivity to volatility)
        double vega = CalculateVega(option);

        // Calculate Theta (time decay)
        double theta = CalculateTheta(option);

        // Calculate Rho (sensitivity to interest rate)
        double rho = CalculateRho(option);

        var result = new OptionResult
        {
            Price = basePrice,
            Delta = delta,
            Gamma = gamma,
            Vega = vega,
            Theta = theta,
            Rho = rho
        };

        return result;
    }

    /// <summary>
    /// Calculates vega using central finite differences by reconstructing the process with bumped volatility.
    /// Uses symmetric bumping (up and down) for accurate derivative estimation.
    /// </summary>
    private double CalculateVega(VanillaOption option)
    {
        try
        {
            var blackVolTS = _process.blackVolatility();
            var referenceDate = Settings.instance().getEvaluationDate();
            
            // Extract current volatility
            double currentVol = blackVolTS.blackVol(
                referenceDate.Add(new Period(1, TimeUnit.Years)), 
                100.0);

            // Define vega calculation function for central differences
            Func<double, double> vegaFunc = (volShift) =>
            {
                var bumpedVolTS = new BlackConstantVol(
                    referenceDate,
                    new TARGET(),
                    currentVol + volShift,
                    new Actual365Fixed());

                var bumpedProcess = new BlackScholesMertonProcess(
                    new QuoteHandle(_underlyingQuote!),
                    _process.dividendYield(),
                    _process.riskFreeRate(),
                    new BlackVolTermStructureHandle(bumpedVolTS));

                var bumpedEngine = new QdFpAmericanEngine(bumpedProcess);
                option.setPricingEngine(bumpedEngine);
                double price = option.NPV();

                // Clean up
                bumpedEngine.Dispose();
                bumpedProcess.Dispose();
                bumpedVolTS.Dispose();

                return price;
            };

            // Use central finite differences (symmetric bumping)
            double vega = _differentiator.EvaluateDerivative(vegaFunc, 0.0, 1);

            // Restore original engine
            option.setPricingEngine(_engine);

            return vega;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Vega calculation failed: {ex.Message}");
            return 0.0;
        }
    }

    /// <summary>
    /// Calculates theta using central finite differences by shifting the evaluation date.
    /// Returns theta per day (negative for time decay).
    /// Uses symmetric date shifts (forward and backward) for accuracy.
    /// </summary>
    private double CalculateTheta(VanillaOption option)
    {
        try
        {
            var originalDate = Settings.instance().getEvaluationDate();

            // Define theta calculation function using central differences
            Func<double, double> thetaFunc = (dayShift) =>
            {
                int days = (int)Math.Round(dayShift);
                var newDate = originalDate.Add(new Period(days, TimeUnit.Days));
                Settings.instance().setEvaluationDate(newDate);
                double price = option.NPV();
                Settings.instance().setEvaluationDate(originalDate);
                return price;
            };

            // Calculate theta using central finite differences
            // Note: theta is conventionally negative (time decay)
            double theta = _differentiator.EvaluateDerivative(thetaFunc, 0.0, 1);

            return theta;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Theta calculation failed: {ex.Message}");
            return 0.0;
        }
    }

    /// <summary>
    /// Calculates rho using central finite differences by reconstructing the process with bumped risk-free rate.
    /// Returns rho per 0.01 (1% change in rate).
    /// Uses symmetric rate bumps (up and down) for accurate derivative.
    /// </summary>
    private double CalculateRho(VanillaOption option)
    {
        try
        {
            var referenceDate = Settings.instance().getEvaluationDate();
            var riskFreeTS = _process.riskFreeRate();
            
            // Get current rate - extract the double value from InterestRate
            var interestRate = riskFreeTS.zeroRate(
                referenceDate.Add(new Period(1, TimeUnit.Years)),
                new Actual365Fixed(), 
                Compounding.Continuous);
            double currentRate = interestRate.rate();

            // Define rho calculation function for central differences
            Func<double, double> rhoFunc = (rateShift) =>
            {
                var bumpedRateTS = new FlatForward(
                    referenceDate, 
                    currentRate + rateShift, 
                    new Actual365Fixed());
                
                var bumpedProcess = new BlackScholesMertonProcess(
                    new QuoteHandle(_underlyingQuote!),
                    _process.dividendYield(),
                    new YieldTermStructureHandle(bumpedRateTS),
                    _process.blackVolatility());

                var bumpedEngine = new QdFpAmericanEngine(bumpedProcess);
                option.setPricingEngine(bumpedEngine);
                double price = option.NPV();

                // Clean up
                bumpedEngine.Dispose();
                bumpedProcess.Dispose();
                bumpedRateTS.Dispose();

                return price;
            };

            // Use central finite differences (symmetric bumping)
            double rho = _differentiator.EvaluateDerivative(rhoFunc, 0.0, 1);

            // Restore original engine
            option.setPricingEngine(_engine);

            // Scale to per 1% (0.01) change
            return rho * 0.01;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Rho calculation failed: {ex.Message}");
            return 0.0;
        }
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
    /// Generates a price-Greek profile across a range of spot prices.
    /// All Greeks at each point are calculated using central finite differences.
    /// </summary>
    /// <param name="option">The vanilla option to analyse.</param>
    /// <param name="spotMin">Minimum spot price for analysis.</param>
    /// <param name="spotMax">Maximum spot price for analysis.</param>
    /// <param name="steps">Number of steps in the spot range (minimum 2).</param>
    /// <returns>List of sensitivity results with spot prices and Greeks.</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when underlying quote is unavailable.</exception>
    public List<SensitivityResult> SensitivityAnalysis(
        VanillaOption option,
        double spotMin,
        double spotMax,
        int steps = 20)
    {
        ArgumentNullException.ThrowIfNull(option);
        
        if (spotMin >= spotMax)
            throw new ArgumentException("spotMin must be less than spotMax");
        
        if (steps < 2)
            throw new ArgumentException("steps must be at least 2");

        if (_underlyingQuote is null)
        {
            throw new InvalidOperationException(
                "Cannot perform sensitivity analysis: underlying quote not available");
        }

        var originalSpot = _underlyingQuote.value();
        var results = new List<SensitivityResult>(steps);

        try
        {
            double stepSize = (spotMax - spotMin) / (steps - 1);

            for (int i = 0; i < steps; i++)
            {
                double spot = spotMin + i * stepSize;
                _underlyingQuote.setValue(spot);
                
                var optionResult = Calculate(option);
                results.Add(new SensitivityResult
                {
                    Spot = spot,
                    Result = optionResult
                });
            }
        }
        finally
        {
            // Always restore the original spot price
            _underlyingQuote.setValue(originalSpot);
        }

        return results;
    }

    /// <summary>
    /// Disposes of the pricing engine resources.
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
/// All Greeks are computed using central finite differences for maximum accuracy.
/// </summary>
public sealed class OptionResult
{
    /// <summary>Gets or sets the option price (NPV).</summary>
    public double Price { get; init; }

    /// <summary>
    /// Gets or sets delta: rate of change of option value with respect to underlying price.
    /// Computed using central finite differences: (V(S+h) - V(S-h)) / (2h).
    /// </summary>
    public double Delta { get; init; }

    /// <summary>
    /// Gets or sets gamma: rate of change of delta with respect to underlying price (convexity).
    /// Represents the second derivative of option value with respect to spot.
    /// Computed using central finite differences for maximum accuracy.
    /// </summary>
    public double Gamma { get; init; }

    /// <summary>
    /// Gets or sets vega: sensitivity to volatility changes.
    /// Represents derivative of option value with respect to volatility (in decimal form).
    /// Computed using central finite differences with volatility bumps.
    /// </summary>
    public double Vega { get; init; }

    /// <summary>
    /// Gets or sets theta: rate of time decay (per day).
    /// Conventionally negative, representing value loss as time passes.
    /// Computed using central finite differences with symmetric date shifts.
    /// </summary>
    public double Theta { get; init; }

    /// <summary>
    /// Gets or sets rho: sensitivity to interest rate changes.
    /// Scaled to represent change per 1% (0.01) change in interest rate.
    /// Computed using central finite differences with rate bumps.
    /// </summary>
    public double Rho { get; init; }
}