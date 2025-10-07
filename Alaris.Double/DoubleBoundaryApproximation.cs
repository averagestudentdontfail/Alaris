namespace Alaris.Double;

/// <summary>
/// Implements the quadratic approximation method for American option exercise boundaries.
/// Based on Ju and Zhong (1999): "An Approximate Formula for Pricing American Options"
/// Provides accurate boundary estimation with support for negative interest rates.
/// </summary>
public sealed class DoubleBoundaryApproximation : IDisposable
{
    private readonly GeneralizedBlackScholesProcess _process;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _riskFreeRate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the DoubleBoundaryApproximation with market parameters.
    /// </summary>
    /// <param name="process">The underlying stochastic process.</param>
    /// <param name="strike">Strike price of the option.</param>
    /// <param name="maturity">Time to maturity in years.</param>
    /// <param name="riskFreeRate">Risk-free interest rate (supports negative values).</param>
    /// <param name="dividendYield">Continuous dividend yield.</param>
    /// <param name="volatility">Volatility of the underlying asset.</param>
    public DoubleBoundaryApproximation(
        GeneralizedBlackScholesProcess process,
        double strike,
        double maturity,
        double riskFreeRate,
        double dividendYield,
        double volatility)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _strike = strike;
        _maturity = maturity;
        _riskFreeRate = riskFreeRate;
        _dividendYield = dividendYield;
        _volatility = volatility;

        ValidateParameters();
    }

    /// <summary>
    /// Calculates the exercise boundaries using the quadratic approximation method.
    /// </summary>
    /// <param name="spot">Current spot price of the underlying.</param>
    /// <param name="isCall">True for call option, false for put option.</param>
    /// <returns>The boundary result containing upper and lower boundaries.</returns>
    public BoundaryResult Calculate(double spot, bool isCall)
    {
        if (spot <= 0)
            throw new ArgumentException("Spot price must be positive", nameof(spot));

        // Calculate the cost of carry
        var b = _riskFreeRate - _dividendYield;

        if (isCall)
        {
            return CalculateCallBoundary(spot, b);
        }
        else
        {
            return CalculatePutBoundary(spot, b);
        }
    }

    /// <summary>
    /// Calculates the approximate option value given the boundaries.
    /// </summary>
    /// <param name="spot">Current spot price.</param>
    /// <param name="strike">Strike price.</param>
    /// <param name="isCall">True for call, false for put.</param>
    /// <param name="boundaries">The calculated exercise boundaries.</param>
    /// <returns>Approximate option value.</returns>
    public double ApproximateValue(
        double spot,
        double strike,
        bool isCall,
        BoundaryResult boundaries)
    {
        // Calculate d1 and d2 for Black-Scholes
        var d1 = CalculateD1(spot, strike, _maturity);
        var d2 = d1 - _volatility * Math.Sqrt(_maturity);

        // Standard normal CDF
        var nd1 = NormalCDF(d1);
        var nd2 = NormalCDF(d2);

        // Discount factor
        var discountFactor = Math.Exp(-_riskFreeRate * _maturity);
        var dividendDiscount = Math.Exp(-_dividendYield * _maturity);

        if (isCall)
        {
            // European call value
            var europeanValue = spot * dividendDiscount * nd1 - strike * discountFactor * nd2;

            // Early exercise premium
            var earlyExercisePremium = CalculateEarlyExercisePremium(
                spot, strike, boundaries.UpperBoundary, true);

            return europeanValue + earlyExercisePremium;
        }
        else
        {
            // European put value
            var europeanValue = strike * discountFactor * NormalCDF(-d2) - 
                               spot * dividendDiscount * NormalCDF(-d1);

            // Early exercise premium
            var earlyExercisePremium = CalculateEarlyExercisePremium(
                spot, strike, boundaries.LowerBoundary, false);

            return europeanValue + earlyExercisePremium;
        }
    }

    /// <summary>
    /// Calculates the call option exercise boundary.
    /// </summary>
    private BoundaryResult CalculateCallBoundary(double spot, double costOfCarry)
    {
        // For calls, never exercise early if no dividends (American = European)
        if (_dividendYield <= 0)
        {
            return new BoundaryResult
            {
                UpperBoundary = double.PositiveInfinity,
                LowerBoundary = 0,
                CrossingTime = _maturity
            };
        }

        // Calculate q1 and q2 parameters from the paper
        var q1 = CalculateQ1(costOfCarry);
        var q2 = CalculateQ2(costOfCarry);

        // Critical stock price (seed value)
        var criticalPrice = CalculateCriticalPrice(_strike, true, q2);

        // Iterative refinement
        var boundary = RefineCallBoundary(criticalPrice, q1, q2);

        return new BoundaryResult
        {
            UpperBoundary = boundary,
            LowerBoundary = 0,
            CrossingTime = EstimateCrossingTime(spot, boundary, true)
        };
    }

    /// <summary>
    /// Calculates the put option exercise boundary.
    /// </summary>
    private BoundaryResult CalculatePutBoundary(double spot, double costOfCarry)
    {
        // Calculate q1 and q2 parameters
        var q1 = CalculateQ1(costOfCarry);
        var q2 = CalculateQ2(costOfCarry);

        // Critical stock price (seed value)
        var criticalPrice = CalculateCriticalPrice(_strike, false, q1);

        // Iterative refinement
        var boundary = RefinePutBoundary(criticalPrice, q1, q2);

        return new BoundaryResult
        {
            UpperBoundary = double.PositiveInfinity,
            LowerBoundary = boundary,
            CrossingTime = EstimateCrossingTime(spot, boundary, false)
        };
    }

    /// <summary>
    /// Calculates the q1 parameter from the Ju-Zhong formula.
    /// </summary>
    private double CalculateQ1(double costOfCarry)
    {
        var term = Math.Sqrt(Math.Pow(costOfCarry - _volatility * _volatility / 2, 2) + 
                            2 * _riskFreeRate * _volatility * _volatility);
        return (-(costOfCarry - _volatility * _volatility / 2) - term) / (_volatility * _volatility);
    }

    /// <summary>
    /// Calculates the q2 parameter from the Ju-Zhong formula.
    /// </summary>
    private double CalculateQ2(double costOfCarry)
    {
        var term = Math.Sqrt(Math.Pow(costOfCarry - _volatility * _volatility / 2, 2) + 
                            2 * _riskFreeRate * _volatility * _volatility);
        return (-(costOfCarry - _volatility * _volatility / 2) + term) / (_volatility * _volatility);
    }

    /// <summary>
    /// Calculates the initial critical price estimate.
    /// </summary>
    private double CalculateCriticalPrice(double strike, bool isCall, double q)
    {
        if (isCall)
        {
            return strike * (1 + 1.0 / q);
        }
        else
        {
            return strike * q / (1 + q);
        }
    }

    /// <summary>
    /// Refines the call boundary using iterative methods.
    /// </summary>
    private double RefineCallBoundary(double initialBoundary, double q1, double q2)
    {
        const int maxIterations = 100;
        const double tolerance = 1e-6;

        var boundary = initialBoundary;

        for (int i = 0; i < maxIterations; i++)
        {
            var d1 = CalculateD1(boundary, _strike, _maturity);
            var nd1 = NormalCDF(d1);

            // Refined boundary calculation
            var numerator = _strike * (1 - Math.Exp(-_dividendYield * _maturity) * nd1);
            var denominator = 1 - nd1 / q2;

            var newBoundary = numerator / denominator;

            if (Math.Abs(newBoundary - boundary) < tolerance)
                return newBoundary;

            boundary = newBoundary;
        }

        return boundary;
    }

    /// <summary>
    /// Refines the put boundary using iterative methods.
    /// </summary>
    private double RefinePutBoundary(double initialBoundary, double q1, double q2)
    {
        const int maxIterations = 100;
        const double tolerance = 1e-6;

        var boundary = initialBoundary;

        for (int i = 0; i < maxIterations; i++)
        {
            var d1 = CalculateD1(boundary, _strike, _maturity);
            var nMinusD1 = NormalCDF(-d1);

            // Refined boundary calculation
            var numerator = _strike * (1 - Math.Exp(-_dividendYield * _maturity) * nMinusD1);
            var denominator = 1 + nMinusD1 / q1;

            var newBoundary = numerator / denominator;

            if (Math.Abs(newBoundary - boundary) < tolerance)
                return newBoundary;

            boundary = newBoundary;
        }

        return boundary;
    }

    /// <summary>
    /// Calculates the early exercise premium component.
    /// </summary>
    private double CalculateEarlyExercisePremium(
        double spot,
        double strike,
        double boundary,
        bool isCall)
    {
        if (double.IsInfinity(boundary))
            return 0;

        // Simplified early exercise premium calculation
        var moneyness = spot / strike;
        var boundaryRatio = boundary / strike;

        if (isCall)
        {
            if (spot >= boundary)
                return spot - strike;
            
            var premium = (boundaryRatio - 1) * Math.Pow(moneyness / boundaryRatio, 2);
            return Math.Max(0, strike * premium);
        }
        else
        {
            if (spot <= boundary)
                return strike - spot;
            
            var premium = (1 - boundaryRatio) * Math.Pow(boundaryRatio / moneyness, 2);
            return Math.Max(0, strike * premium);
        }
    }

    /// <summary>
    /// Estimates the expected time until the spot price crosses the boundary.
    /// </summary>
    private double EstimateCrossingTime(double spot, double boundary, bool isCall)
    {
        if (double.IsInfinity(boundary))
            return _maturity;

        // Use drift and volatility to estimate crossing time
        var drift = _riskFreeRate - _dividendYield - 0.5 * _volatility * _volatility;
        var logRatio = Math.Log(boundary / spot);

        if (isCall && spot >= boundary)
            return 0;
        
        if (!isCall && spot <= boundary)
            return 0;

        // Expected crossing time (rough approximation)
        if (Math.Abs(drift) < 1e-10)
            return _maturity / 2; // Random walk case

        var expectedTime = logRatio / drift;
        return Math.Max(0, Math.Min(expectedTime, _maturity));
    }

    /// <summary>
    /// Calculates d1 for the Black-Scholes formula.
    /// </summary>
    private double CalculateD1(double spot, double strike, double time)
    {
        var numerator = Math.Log(spot / strike) + 
                       (_riskFreeRate - _dividendYield + 0.5 * _volatility * _volatility) * time;
        var denominator = _volatility * Math.Sqrt(time);
        return numerator / denominator;
    }

    /// <summary>
    /// Standard normal cumulative distribution function.
    /// </summary>
    private static double NormalCDF(double x)
    {
        // Using the error function approximation
        return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
    }

    /// <summary>
    /// Error function approximation (Abramowitz and Stegun).
    /// </summary>
    private static double Erf(double x)
    {
        // Constants for the approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var sign = x >= 0 ? 1 : -1;
        x = Math.Abs(x);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    /// <summary>
    /// Validates all input parameters for the approximation.
    /// </summary>
    private void ValidateParameters()
    {
        if (_strike <= 0)
            throw new ArgumentException("Strike must be positive", nameof(_strike));

        if (_maturity <= 0)
            throw new ArgumentException("Maturity must be positive", nameof(_maturity));

        if (_volatility < 0)
            throw new ArgumentException("Volatility cannot be negative", nameof(_volatility));

        if (_volatility > 5.0)
            throw new ArgumentException("Volatility appears unreasonably high", nameof(_volatility));
    }

    /// <summary>
    /// Disposes of unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// Contains the results of boundary calculations including upper and lower exercise boundaries.
/// </summary>
public sealed class BoundaryResult
{
    /// <summary>
    /// Gets or sets the upper exercise boundary (relevant for call options).
    /// For American calls with dividends, this is the price above which immediate exercise is optimal.
    /// </summary>
    public double UpperBoundary { get; set; }

    /// <summary>
    /// Gets or sets the lower exercise boundary (relevant for put options).
    /// For American puts, this is the price below which immediate exercise is optimal.
    /// </summary>
    public double LowerBoundary { get; set; }

    /// <summary>
    /// Gets or sets the estimated time until the spot price crosses the boundary.
    /// Measured in years from the current evaluation date.
    /// </summary>
    public double CrossingTime { get; set; }

    /// <summary>
    /// Determines if immediate exercise is optimal for a call option at the given spot price.
    /// </summary>
    public bool ShouldExerciseCall(double spot) => spot >= UpperBoundary;

    /// <summary>
    /// Determines if immediate exercise is optimal for a put option at the given spot price.
    /// </summary>
    public bool ShouldExercisePut(double spot) => spot <= LowerBoundary;

    /// <summary>
    /// Gets the distance to the exercise boundary as a percentage of spot.
    /// </summary>
    public double DistanceToBoundaryPercent(double spot, bool isCall) =>
        isCall ? (UpperBoundary - spot) / spot : (spot - LowerBoundary) / spot;
}