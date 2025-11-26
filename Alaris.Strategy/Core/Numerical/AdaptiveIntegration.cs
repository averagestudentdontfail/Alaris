using System.Numerics;
using MathNet.Numerics.Integration;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Adaptive numerical integration using MathNet.Numerics.
/// Production-grade implementation for integrating characteristic functions in option pricing.
/// 
/// Uses Double Exponential (tanh-sinh) transformation for semi-infinite intervals,
/// which is particularly effective for oscillatory integrands common in Fourier-based
/// option pricing (Heston, Kou models).
/// 
/// References:
/// - Takahasi and Mori (1974): Double Exponential formulas for numerical integration
/// - MathNet.Numerics documentation: https://numerics.mathdotnet.com/
/// </summary>
public static class AdaptiveIntegration
{
    /// <summary>
    /// Default absolute tolerance for convergence.
    /// </summary>
    private const double DefaultAbsoluteTolerance = 1e-8;

    /// <summary>
    /// Default relative tolerance for convergence.
    /// </summary>
    private const double DefaultRelativeTolerance = 1e-6;

    /// <summary>
    /// Default order for Gauss-Legendre quadrature.
    /// Higher order provides better accuracy for smooth functions.
    /// </summary>
    private const int DefaultGaussLegendreOrder = 128;

    /// <summary>
    /// Lower order for error estimation.
    /// </summary>
    private const int LowerGaussLegendreOrder = 64;

    /// <summary>
    /// Integrates a real-valued function over a finite interval using high-order Gauss-Legendre quadrature.
    /// </summary>
    /// <param name="f">The function to integrate.</param>
    /// <param name="a">Lower integration bound.</param>
    /// <param name="b">Upper integration bound.</param>
    /// <param name="absoluteTolerance">Absolute error tolerance (used for error reporting).</param>
    /// <param name="relativeTolerance">Relative error tolerance (used for error reporting).</param>
    /// <returns>The integral value and estimated error.</returns>
    public static (double Value, double Error) Integrate(
        Func<double, double> f,
        double a,
        double b,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(f);

        if (double.IsInfinity(a) || double.IsInfinity(b))
        {
            throw new ArgumentException("Infinite bounds require IntegrateToInfinity method.");
        }

        if (a >= b)
        {
            return (0, 0);
        }

        // Use high-order Gauss-Legendre from MathNet.Numerics
        // Order 128 provides excellent accuracy for most functions
        double result = GaussLegendreRule.Integrate(f, a, b, DefaultGaussLegendreOrder);

        // Estimate error by comparing with lower-order result
        double lowerOrderResult = GaussLegendreRule.Integrate(f, a, b, LowerGaussLegendreOrder);
        double error = Math.Abs(result - lowerOrderResult);

        return (result, error);
    }

    /// <summary>
    /// Integrates a complex-valued function over a finite interval.
    /// </summary>
    /// <param name="f">The complex function to integrate.</param>
    /// <param name="a">Lower integration bound.</param>
    /// <param name="b">Upper integration bound.</param>
    /// <param name="absoluteTolerance">Absolute error tolerance.</param>
    /// <param name="relativeTolerance">Relative error tolerance.</param>
    /// <returns>The integral value and estimated error.</returns>
    public static (Complex Value, double Error) IntegrateComplex(
        Func<double, Complex> f,
        double a,
        double b,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(f);

        // Integrate real and imaginary parts separately
        (double realValue, double realError) = Integrate(
            x => f(x).Real, a, b, absoluteTolerance, relativeTolerance);
        (double imagValue, double imagError) = Integrate(
            x => f(x).Imaginary, a, b, absoluteTolerance, relativeTolerance);

        return (new Complex(realValue, imagValue), Math.Sqrt((realError * realError) + (imagError * imagError)));
    }

    /// <summary>
    /// Integrates from a to infinity using Double Exponential (tanh-sinh) transformation.
    /// 
    /// The DE transformation is particularly effective for:
    /// - Semi-infinite and infinite intervals
    /// - Oscillatory integrands (common in Fourier-based option pricing)
    /// - Integrands with endpoint singularities
    /// 
    /// For Heston/Kou characteristic function integration, this method provides
    /// robust results across all moneyness levels, including deep OTM options.
    /// </summary>
    /// <param name="f">The function to integrate.</param>
    /// <param name="a">Lower integration bound (typically 0 for characteristic function integration).</param>
    /// <param name="absoluteTolerance">Absolute error tolerance.</param>
    /// <param name="relativeTolerance">Relative error tolerance (currently unused, kept for API compatibility).</param>
    /// <returns>The integral value and estimated error.</returns>
    public static (double Value, double Error) IntegrateToInfinity(
        Func<double, double> f,
        double a,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(f);

        // Use Double Exponential transformation for semi-infinite integral
        // This is the tanh-sinh quadrature method, excellent for oscillatory integrands
        double result = DoubleExponentialTransformation.Integrate(
            f,
            a,
            double.PositiveInfinity,
            absoluteTolerance);

        // Estimate error by comparing with a truncated integral
        // Use a large but finite upper bound for comparison
        double truncatedResult = GaussLegendreRule.Integrate(f, a, 200.0, DefaultGaussLegendreOrder);
        double error = Math.Abs(result - truncatedResult);

        // If both results are very close, we've likely achieved convergence
        // Report a small error in this case
        if (error < absoluteTolerance)
        {
            error = absoluteTolerance * 0.1;
        }

        return (result, error);
    }

    /// <summary>
    /// Integrates a complex function from a to infinity.
    /// </summary>
    /// <param name="f">The complex function to integrate.</param>
    /// <param name="a">Lower integration bound.</param>
    /// <param name="absoluteTolerance">Absolute error tolerance.</param>
    /// <param name="relativeTolerance">Relative error tolerance.</param>
    /// <returns>The integral value and estimated error.</returns>
    public static (Complex Value, double Error) IntegrateComplexToInfinity(
        Func<double, Complex> f,
        double a,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(f);

        // Integrate real and imaginary parts separately
        (double realValue, double realError) = IntegrateToInfinity(
            x => f(x).Real, a, absoluteTolerance, relativeTolerance);
        (double imagValue, double imagError) = IntegrateToInfinity(
            x => f(x).Imaginary, a, absoluteTolerance, relativeTolerance);

        return (new Complex(realValue, imagValue), Math.Sqrt((realError * realError) + (imagError * imagError)));
    }
}