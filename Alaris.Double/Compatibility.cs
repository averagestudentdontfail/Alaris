using Alaris.Quantlib;

namespace Alaris.Double;

/// <summary>
/// Simplified compatibility layer - eliminates most of the complex SWIG workarounds
/// Only keeps essential helpers that are actually needed
/// </summary>
public static class SimplifiedQuantLibHelper
{
    // Cache commonly used objects to avoid repeated instantiation
    private static readonly CumulativeNormalDistribution _normalCdf = new();
    
    /// <summary>
    /// Simplified normal CDF access - replaces complex reflection-based calls
    /// </summary>
    public static double NormalCdf(double x)
    {
        return _normalCdf.value(x);
    }

    /// <summary>
    /// Simplified rate extraction from QuantLib term structures
    /// </summary>
    public static double ExtractRate(YieldTermStructure termStructure, double timeToMaturity)
    {
        try
        {
            return termStructure.forwardRate(0.0, timeToMaturity, Compounding.Continuous).rate();
        }
        catch
        {
            // Fallback for edge cases
            return termStructure.zeroRate(timeToMaturity, Compounding.Continuous).rate();
        }
    }

    /// <summary>
    /// Simplified volatility extraction
    /// </summary>
    public static double ExtractVolatility(BlackVolTermStructure volStructure, double timeToMaturity, double strike)
    {
        return volStructure.blackVol(timeToMaturity, strike);
    }

    /// <summary>
    /// Simple trapezoidal integration - only for absolute fallback cases
    /// </summary>
    public static double SimpleTrapezoidalIntegration(Func<double, double> f, double a, double b, int n = 1000)
    {
        double h = (b - a) / n;
        double sum = 0.5 * (SafeEvaluate(f, a) + SafeEvaluate(f, b));

        for (int i = 1; i < n; i++)
        {
            sum += SafeEvaluate(f, a + i * h);
        }

        return h * sum;
    }

    /// <summary>
    /// Safe function evaluation with error handling
    /// </summary>
    private static double SafeEvaluate(Func<double, double> f, double x)
    {
        try
        {
            var result = f(x);
            return double.IsNaN(result) || double.IsInfinity(result) ? 0.0 : result;
        }
        catch
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Create QuantLib Date from .NET DateTime
    /// </summary>
    public static Date CreateQuantLibDate(DateTime dateTime)
    {
        return new Date((uint)dateTime.Day, (Month)(dateTime.Month), (uint)dateTime.Year);
    }

    /// <summary>
    /// Convert QuantLib Date to .NET DateTime
    /// </summary>
    public static DateTime ConvertToDateTime(Date quantLibDate)
    {
        return new DateTime((int)quantLibDate.year(), (int)quantLibDate.month(), (int)quantLibDate.dayOfMonth());
    }
}

/// <summary>
/// Minimal parameter validation - replaces complex validation in original files
/// </summary>
public static class ParameterValidator
{
    public static void ValidateMarketParameters(double spot, double strike, double r, double q, double sigma, double timeToMaturity)
    {
        if (spot <= 0) throw new ArgumentException("Spot price must be positive");
        if (strike <= 0) throw new ArgumentException("Strike price must be positive");
        if (sigma <= 0) throw new ArgumentException("Volatility must be positive");
        if (timeToMaturity <= 0) throw new ArgumentException("Time to maturity must be positive");
        if (sigma > 5.0) throw new ArgumentException("Volatility too high (>500%)");
        if (Math.Abs(r) > 0.5) throw new ArgumentException("Interest rate too extreme");
        if (Math.Abs(q) > 0.5) throw new ArgumentException("Dividend yield too extreme");
    }

    public static void ValidateSpectralParameters(int spectralNodes, double tolerance, int maxIterations)
    {
        if (spectralNodes < 3 || spectralNodes > 32) 
            throw new ArgumentException("Spectral nodes must be between 3 and 32");
        if (tolerance <= 0 || tolerance > 1e-3) 
            throw new ArgumentException("Tolerance must be between 0 and 1e-3");
        if (maxIterations < 1 || maxIterations > 1000) 
            throw new ArgumentException("Max iterations must be between 1 and 1000");
    }
}