using System.Numerics;

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Adaptive numerical integration using Gauss-Kronrod quadrature.
/// Production-grade implementation for integrating characteristic functions in option pricing.
/// </summary>
public static class AdaptiveIntegration
{
    /// <summary>
    /// Maximum number of subdivisions for adaptive integration.
    /// </summary>
    private const int MaxSubdivisions = 1000;

    /// <summary>
    /// Default absolute tolerance for convergence.
    /// </summary>
    private const double DefaultAbsoluteTolerance = 1e-8;

    /// <summary>
    /// Default relative tolerance for convergence.
    /// </summary>
    private const double DefaultRelativeTolerance = 1e-6;

    /// <summary>
    /// Gauss-Kronrod 15-point nodes (normalized to [-1, 1]).
    /// </summary>
    private static readonly double[] GK15Nodes =
    {
        -0.9914553711208126, -0.9491079123427585, -0.8648644233597691,
        -0.7415311855993944, -0.5860872354676911, -0.4058451513773972,
        -0.2077849550078985, 0.0000000000000000, 0.2077849550078985,
        0.4058451513773972, 0.5860872354676911, 0.7415311855993944,
        0.8648644233597691, 0.9491079123427585, 0.9914553711208126
    };

    /// <summary>
    /// Gauss-Kronrod 15-point weights.
    /// </summary>
    private static readonly double[] GK15Weights =
    {
        0.0229353220105292, 0.0630920926299786, 0.1047900103222502,
        0.1406532597155259, 0.1690047266392679, 0.1903505780647854,
        0.2044329400752989, 0.2094821410847278, 0.2044329400752989,
        0.1903505780647854, 0.1690047266392679, 0.1406532597155259,
        0.1047900103222502, 0.0630920926299786, 0.0229353220105292
    };

    /// <summary>
    /// Gauss 7-point weights (subset of GK15 for error estimation).
    /// </summary>
    private static readonly double[] G7Weights =
    {
        0.0, 0.1294849661688697, 0.0, 0.2797053914892767, 0.0,
        0.3818300505051189, 0.0, 0.4179591836734694, 0.0,
        0.3818300505051189, 0.0, 0.2797053914892767, 0.0,
        0.1294849661688697, 0.0
    };

    /// <summary>
    /// Integrates a real-valued function using adaptive Gauss-Kronrod quadrature.
    /// </summary>
    /// <param name="f">The function to integrate.</param>
    /// <param name="a">Lower integration bound.</param>
    /// <param name="b">Upper integration bound.</param>
    /// <param name="absoluteTolerance">Absolute error tolerance.</param>
    /// <param name="relativeTolerance">Relative error tolerance.</param>
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
            throw new ArgumentException("Infinite bounds require transformation.");
        }

        if (a >= b)
        {
            return (0, 0);
        }

        // Use a priority queue to adaptively subdivide intervals
        var intervals = new SortedSet<IntegrationInterval>(
            Comparer<IntegrationInterval>.Create((x, y) =>
                y.Error.CompareTo(x.Error) != 0 ? y.Error.CompareTo(x.Error) : x.Left.CompareTo(y.Left)));

        // Compute initial interval
        IntegrationInterval initial = ComputeInterval(f, a, b);
        intervals.Add(initial);

        double totalValue = initial.Value;
        double totalError = initial.Error;

        int subdivisions = 0;

        while (subdivisions < MaxSubdivisions &&
               totalError > absoluteTolerance &&
               totalError > relativeTolerance * Math.Abs(totalValue))
        {
            // Take interval with largest error
            IntegrationInterval worst = intervals.Max!;
            intervals.Remove(worst);

            // Subdivide
            double mid = (worst.Left + worst.Right) / 2;
            IntegrationInterval leftHalf = ComputeInterval(f, worst.Left, mid);
            IntegrationInterval rightHalf = ComputeInterval(f, mid, worst.Right);

            // Update totals
            totalValue = totalValue - worst.Value + leftHalf.Value + rightHalf.Value;
            totalError = totalError - worst.Error + leftHalf.Error + rightHalf.Error;

            intervals.Add(leftHalf);
            intervals.Add(rightHalf);
            subdivisions++;
        }

        return (totalValue, totalError);
    }

    /// <summary>
    /// Integrates a complex-valued function using adaptive Gauss-Kronrod quadrature.
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
        var (realValue, realError) = Integrate(x => f(x).Real, a, b, absoluteTolerance, relativeTolerance);
        var (imagValue, imagError) = Integrate(x => f(x).Imaginary, a, b, absoluteTolerance, relativeTolerance);

        return (new Complex(realValue, imagValue), Math.Sqrt(realError * realError + imagError * imagError));
    }

    /// <summary>
    /// Computes the Gauss-Kronrod quadrature over a single interval.
    /// </summary>
    private static IntegrationInterval ComputeInterval(Func<double, double> f, double a, double b)
    {
        double halfLength = (b - a) / 2;
        double center = (a + b) / 2;

        double gk15Sum = 0;
        double g7Sum = 0;

        for (int i = 0; i < GK15Nodes.Length; i++)
        {
            double x = center + halfLength * GK15Nodes[i];
            double fx = f(x);

            gk15Sum += GK15Weights[i] * fx;
            g7Sum += G7Weights[i] * fx;
        }

        double value = halfLength * gk15Sum;
        double gaussValue = halfLength * g7Sum;

        // Error estimate from difference between GK15 and G7
        double error = Math.Abs(value - gaussValue);

        return new IntegrationInterval(a, b, value, error);
    }

    /// <summary>
    /// Represents an integration interval with computed value and error.
    /// </summary>
    private readonly struct IntegrationInterval
    {
        public double Left { get; }
        public double Right { get; }
        public double Value { get; }
        public double Error { get; }

        public IntegrationInterval(double left, double right, double value, double error)
        {
            Left = left;
            Right = right;
            Value = value;
            Error = error;
        }
    }

    /// <summary>
    /// Integrates from a to infinity using exponential transformation.
    /// Transform: x = a + exp(t), dx = exp(t)dt
    /// </summary>
    public static (double Value, double Error) IntegrateToInfinity(
        Func<double, double> f,
        double a,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(f);

        // Transform to finite interval: integrate from -inf to some large value
        // We use x = a + exp(t), so t ranges from -inf to some T
        // In practice, we integrate t from -10 to 10 (covers exp(-10) ≈ 0 to exp(10) ≈ 22000)

        double Transformed(double t)
        {
            double expT = Math.Exp(t);
            double x = a + expT;
            return f(x) * expT; // Jacobian
        }

        // Determine upper bound adaptively
        // Find where integrand becomes negligible
        double upperBound = 5;
        for (double t = 0; t <= 20; t += 0.5)
        {
            if (Math.Abs(Transformed(t)) < absoluteTolerance * 0.01)
            {
                upperBound = t;
                break;
            }
        }

        return Integrate(Transformed, -10, upperBound, absoluteTolerance, relativeTolerance);
    }

    /// <summary>
    /// Integrates a complex function from a to infinity.
    /// </summary>
    public static (Complex Value, double Error) IntegrateComplexToInfinity(
        Func<double, Complex> f,
        double a,
        double absoluteTolerance = DefaultAbsoluteTolerance,
        double relativeTolerance = DefaultRelativeTolerance)
    {
        ArgumentNullException.ThrowIfNull(f);

        Complex Transformed(double t)
        {
            double expT = Math.Exp(t);
            double x = a + expT;
            return f(x) * expT; // Jacobian
        }

        // Determine upper bound adaptively
        double upperBound = 5;
        for (double t = 0; t <= 20; t += 0.5)
        {
            if (Transformed(t).Magnitude < absoluteTolerance * 0.01)
            {
                upperBound = t;
                break;
            }
        }

        return IntegrateComplex(Transformed, -10, upperBound, absoluteTolerance, relativeTolerance);
    }
}
