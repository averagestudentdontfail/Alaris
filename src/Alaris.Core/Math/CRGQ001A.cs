// CRGQ001A.cs - Gauss Quadrature Infrastructure
// Component ID: CRGQ001A
//
// Provides Gauss-Legendre and Tanh-Sinh (doubly exponential) quadrature
// for high-precision numerical integration in American option pricing.
//
// References:
// - Golub & Welsch (1969) "Calculation of Gauss Quadrature Rules"
// - Takahasi & Mori (1974) "Double Exponential Formulas for Numerical Integration"
// - Andersen, Lake & Offengenden (2016) "High Performance American Option Pricing"

using System;
using System.Runtime.CompilerServices;

namespace Alaris.Core.Math;

/// <summary>
/// Gauss quadrature infrastructure for spectral integration methods.
/// </summary>
public static class CRGQ001A
{
    private const int MaxLegendreOrder = 64;
    private const int MaxLaguerreOrder = 32;
    private const int MaxHermiteOrder = 32;
    private const double LegendreRootTolerance = 1e-15;
    private const int LegendreMaxIterations = 100;
    private const double TanhSinhWeightCutoff = 1e-20;
    private const double TanhSinhOverflowLimit = 350.0;
    private const int TanhSinhMaxSamples = 1000;

    /// <summary>
    /// Pre-computed Gauss-Legendre nodes and weights for common orders.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Nested class provides logical grouping for pre-computed schemes")]
    public static class Schemes
    {
        /// <summary>Fast scheme: 8-point Gauss-Legendre.</summary>
        public static readonly (double[] Nodes, double[] Weights) Fast = GaussLegendre(8);

        /// <summary>Accurate scheme: 16-point Gauss-Legendre.</summary>
        public static readonly (double[] Nodes, double[] Weights) Accurate = GaussLegendre(16);

        /// <summary>High precision scheme: 32-point Gauss-Legendre.</summary>
        public static readonly (double[] Nodes, double[] Weights) HighPrecision = GaussLegendre(32);

        /// <summary>Adaptive scheme: 7-point Gauss-Legendre.</summary>
        public static readonly (double[] Nodes, double[] Weights) Adaptive7 = GaussLegendre(7);

        /// <summary>Adaptive scheme: 15-point Gauss-Legendre.</summary>
        public static readonly (double[] Nodes, double[] Weights) Adaptive15 = GaussLegendre(15);
    }

    /// <summary>
    /// Computes Gauss-Legendre nodes and weights on [-1, 1].
    /// </summary>
    /// <param name="n">Number of quadrature points (1 to 64).</param>
    /// <returns>Tuple of (nodes, weights) arrays.</returns>
    /// <remarks>
    /// Uses Newton-Raphson iteration to find roots of Legendre polynomials.
    /// Weights are computed from the derivative formula.
    /// </remarks>
    public static (double[] Nodes, double[] Weights) GaussLegendre(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxLegendreOrder);

        double[] nodes = new double[n];
        double[] weights = new double[n];

        int m = (n + 1) / 2;  // Number of positive roots (symmetric)

        for (int i = 0; i < m; i++)
        {
            // Initial guess using asymptotic formula
            double z = System.Math.Cos(System.Math.PI * (i + 0.75) / (n + 0.5));

            double z1, pp;
            int iter = 0;

            do
            {
                // Evaluate Legendre polynomial and its derivative
                double p1 = 1.0;
                double p2 = 0.0;

                for (int j = 0; j < n; j++)
                {
                    double p3 = p2;
                    p2 = p1;
                    p1 = (((2.0 * j) + 1.0) * z * p2 - j * p3) / (j + 1);
                }

                // p1 is now P_n(z), derivative using recurrence
                pp = n * ((z * p1) - p2) / ((z * z) - 1.0);

                z1 = z;
                z -= p1 / pp;
                iter++;
            }
            while (System.Math.Abs(z - z1) > LegendreRootTolerance && iter < LegendreMaxIterations);

            // Store symmetric pairs
            nodes[i] = -z;
            nodes[n - 1 - i] = z;

            // Weight formula
            double weight = 2.0 / (((1.0 - (z * z)) * pp) * pp);
            weights[i] = weight;
            weights[n - 1 - i] = weight;
        }

        return (nodes, weights);
    }

    /// <summary>
    /// Transforms Gauss-Legendre nodes and weights to interval [a, b].
    /// </summary>
    /// <param name="n">Number of quadrature points.</param>
    /// <param name="a">Left endpoint.</param>
    /// <param name="b">Right endpoint.</param>
    /// <returns>Transformed (nodes, weights) for integration over [a, b].</returns>
    public static (double[] Nodes, double[] Weights) GaussLegendreAB(int n, double a, double b)
    {
        (double[] nodes, double[] weights) = GaussLegendre(n);

        double halfWidth = (b - a) / 2.0;
        double midPoint = (a + b) / 2.0;

        for (int i = 0; i < n; i++)
        {
            nodes[i] = midPoint + (halfWidth * nodes[i]);
            weights[i] *= halfWidth;
        }

        return (nodes, weights);
    }

    /// <summary>
    /// Integrates a function using Gauss-Legendre quadrature.
    /// </summary>
    /// <param name="f">Function to integrate.</param>
    /// <param name="a">Left endpoint.</param>
    /// <param name="b">Right endpoint.</param>
    /// <param name="n">Number of quadrature points.</param>
    /// <returns>Approximate integral value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GaussLegendreIntegrate(Func<double, double> f, double a, double b, int n = 16)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (a == b)
        {
            return 0.0;
        }

        (double[] nodes, double[] weights) = GaussLegendreAB(n, a, b);

        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            sum += weights[i] * f(nodes[i]);
        }

        return sum;
    }

    /// <summary>
    /// Integrates a function using Tanh-Sinh (doubly exponential) quadrature.
    /// </summary>
    /// <param name="f">Function to integrate.</param>
    /// <param name="a">Left endpoint.</param>
    /// <param name="b">Right endpoint.</param>
    /// <param name="tolerance">Desired relative tolerance.</param>
    /// <param name="maxLevels">Maximum refinement levels (default 10).</param>
    /// <returns>Approximate integral value.</returns>
    /// <remarks>
    /// Tanh-Sinh is excellent for integrands with endpoint singularities.
    /// Uses the transformation x = tanh(π/2 * sinh(t)) which clusters points
    /// near endpoints where singularities typically occur.
    /// </remarks>
    public static double TanhSinhIntegrate(
        Func<double, double> f,
        double a,
        double b,
        double tolerance = 1e-10,
        int maxLevels = 10)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLevels, 1);
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
        }

        if (a == b)
        {
            return 0.0;
        }

        double halfWidth = (b - a) / 2.0;
        double midPoint = (a + b) / 2.0;
        double piOverTwo = System.Math.PI / 2.0;
        double h = 1.0;  // Step size
        double previousResult = 0.0;

        for (int level = 0; level < maxLevels; level++)
        {
            double sum = 0.0;
            for (int k = 0; k <= TanhSinhMaxSamples; k++)
            {
                double t = k * h;

                double sinhT = System.Math.Sinh(t);
                double piHalfSinhT = piOverTwo * sinhT;
                if (System.Math.Abs(piHalfSinhT) > TanhSinhOverflowLimit)
                {
                    break;
                }

                double coshT = System.Math.Cosh(t);
                double x = System.Math.Tanh(piHalfSinhT);
                double coshPiHalf = System.Math.Cosh(piHalfSinhT);
                double w = piOverTwo * coshT / (coshPiHalf * coshPiHalf);

                if (w < TanhSinhWeightCutoff)
                {
                    break;
                }

                if (k == 0)
                {
                    double gVal = f(midPoint + (halfWidth * x));
                    if (!double.IsNaN(gVal) && !double.IsInfinity(gVal))
                    {
                        sum += w * gVal;
                    }
                }
                else
                {
                    double gPos = f(midPoint + (halfWidth * x));
                    double gNeg = f(midPoint - (halfWidth * x));

                    if (!double.IsNaN(gPos) && !double.IsInfinity(gPos))
                    {
                        sum += w * gPos;
                    }

                    if (!double.IsNaN(gNeg) && !double.IsInfinity(gNeg))
                    {
                        sum += w * gNeg;
                    }
                }
            }

            double result = h * sum * halfWidth;

            // Check convergence
            if (level > 0 && System.Math.Abs(result - previousResult) < tolerance * System.Math.Abs(result))
            {
                return result;
            }

            previousResult = result;
            h /= 2.0;
        }

        return previousResult;
    }

    /// <summary>
    /// Gauss-Kronrod adaptive quadrature for automatic precision control.
    /// </summary>
    /// <param name="f">Function to integrate.</param>
    /// <param name="a">Left endpoint.</param>
    /// <param name="b">Right endpoint.</param>
    /// <param name="tolerance">Desired relative tolerance.</param>
    /// <param name="maxDepth">Maximum recursion depth.</param>
    /// <returns>Approximate integral value.</returns>
    /// <remarks>
    /// Uses 7-point Gauss and 15-point Kronrod rules for error estimation.
    /// Adaptively subdivides intervals where error is large.
    /// </remarks>
    public static double AdaptiveIntegrate(
        Func<double, double> f,
        double a,
        double b,
        double tolerance = 1e-10,
        int maxDepth = 15)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
        }

        if (a == b)
        {
            return 0.0;
        }

        return AdaptiveIntegrateRecursive(f, a, b, tolerance, maxDepth, 0);
    }

    private static double AdaptiveIntegrateRecursive(
        Func<double, double> f,
        double a,
        double b,
        double tolerance,
        int maxDepth,
        int depth)
    {
        // 7-point Gauss rule
        double integral7 = IntegrateWithScheme(f, Schemes.Adaptive7, a, b);
        double integral15 = IntegrateWithScheme(f, Schemes.Adaptive15, a, b);

        double error = System.Math.Abs(integral15 - integral7);
        double absIntegral = System.Math.Abs(integral15);

        // Check if error is acceptable
        if (error < tolerance * absIntegral || depth >= maxDepth)
        {
            return integral15;
        }

        // Subdivide
        double mid = (a + b) / 2.0;
        double left = AdaptiveIntegrateRecursive(f, a, mid, tolerance / 2.0, maxDepth, depth + 1);
        double right = AdaptiveIntegrateRecursive(f, mid, b, tolerance / 2.0, maxDepth, depth + 1);

        return left + right;
    }

    /// <summary>
    /// Integrates using pre-computed nodes and weights (fastest method).
    /// </summary>
    /// <param name="f">Function to integrate.</param>
    /// <param name="scheme">Pre-computed (nodes, weights) from Schemes class.</param>
    /// <param name="a">Left endpoint.</param>
    /// <param name="b">Right endpoint.</param>
    /// <returns>Approximate integral value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double IntegrateWithScheme(
        Func<double, double> f,
        (double[] Nodes, double[] Weights) scheme,
        double a,
        double b)
    {
        ArgumentNullException.ThrowIfNull(f);
        if (a == b)
        {
            return 0.0;
        }

        double halfWidth = (b - a) / 2.0;
        double midPoint = (a + b) / 2.0;

        double sum = 0.0;
        int n = scheme.Nodes.Length;

        for (int i = 0; i < n; i++)
        {
            double x = midPoint + (halfWidth * scheme.Nodes[i]);
            sum += scheme.Weights[i] * f(x);
        }

        return halfWidth * sum;
    }

    /// <summary>
    /// Computes Gauss-Laguerre nodes and weights for semi-infinite integrals.
    /// </summary>
    /// <param name="n">Number of quadrature points.</param>
    /// <returns>Nodes and weights for integrals over [0, ∞) with weight e^(-x).</returns>
    /// <remarks>
    /// Useful for integrals of the form ∫₀^∞ f(x) e^(-x) dx.
    /// </remarks>
    public static (double[] Nodes, double[] Weights) GaussLaguerre(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxLaguerreOrder);

        double[] nodes = new double[n];
        double[] weights = new double[n];

        for (int i = 0; i < n; i++)
        {
            // Initial guess
            double z;
            if (i == 0)
            {
                z = 3.0 / (1.0 + (2.4 * n));
            }
            else if (i == 1)
            {
                z = nodes[0] + (15.0 / (1.0 + (2.5 * n)));
            }
            else
            {
                double ratio = (1.0 + (2.55 * (i - 1))) / (1.9 * (i - 1));
                z = nodes[i - 1] + (ratio * (nodes[i - 1] - nodes[i - 2]));
            }

            // Newton-Raphson
            int maxIter = 100;
            for (int iter = 0; iter < maxIter; iter++)
            {
                double p1 = 1.0;
                double p2 = 0.0;

                for (int j = 0; j < n; j++)
                {
                    double p3 = p2;
                    p2 = p1;
                    p1 = (((((2 * j) + 1) - z) * p2) - (j * p3)) / (j + 1);
                }

                double pp = (n * p1 - n * p2) / z;
                double z1 = z;
                z -= p1 / pp;

                if (System.Math.Abs(z - z1) < 1e-15)
                {
                    break;
                }
            }

            nodes[i] = z;

            // Evaluate L_{n-1} at z
            double lnm1 = 0.0;
            double ln = 1.0;
            for (int j = 0; j < n; j++)
            {
                double tmp = ln;
                ln = (((((2 * j) + 1) - z) * ln) - (j * lnm1)) / (j + 1);
                lnm1 = tmp;
            }

            weights[i] = z / ((n + 1) * (n + 1) * lnm1 * lnm1);
        }

        return (nodes, weights);
    }

    /// <summary>
    /// Computes Gauss-Hermite nodes and weights for infinite integrals.
    /// </summary>
    /// <param name="n">Number of quadrature points.</param>
    /// <returns>Nodes and weights for integrals over (-∞, ∞) with weight e^(-x²).</returns>
    /// <remarks>
    /// Useful for integrals of the form ∫_{-∞}^∞ f(x) e^(-x²) dx.
    /// </remarks>
    public static (double[] Nodes, double[] Weights) GaussHermite(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxHermiteOrder);

        double[] nodes = new double[n];
        double[] weights = new double[n];

        int m = (n + 1) / 2;

        for (int i = 0; i < m; i++)
        {
            // Initial guess
            double z;
            if (i == 0)
            {
                z = System.Math.Sqrt(2.0 * n + 1.0) - (1.85575 * System.Math.Pow(2.0 * n + 1.0, -1.0 / 6.0));
            }
            else if (i == 1)
            {
                z = nodes[0] - (1.14 * System.Math.Pow(n, 0.426) / nodes[0]);
            }
            else if (i == 2)
            {
                z = (1.86 * nodes[1]) - (0.86 * nodes[0]);
            }
            else if (i == 3)
            {
                z = (1.91 * nodes[2]) - (0.91 * nodes[1]);
            }
            else
            {
                z = (2.0 * nodes[i - 1]) - nodes[i - 2];
            }

            // Newton-Raphson
            int maxIter = 100;
            double p2Final = 0.0;
            for (int iter = 0; iter < maxIter; iter++)
            {
                double p1 = System.Math.Sqrt(System.Math.PI) * 0.7511255444649425;  // 1/π^(1/4)
                double p2 = 0.0;

                for (int j = 0; j < n; j++)
                {
                    double p3 = p2;
                    p2 = p1;
                    p1 = (z * System.Math.Sqrt(2.0 / (j + 1)) * p2) - (System.Math.Sqrt((double)j / (j + 1)) * p3);
                }

                p2Final = p2;
                double pp = System.Math.Sqrt(2.0 * n) * p2;
                double z1 = z;
                z -= p1 / pp;

                if (System.Math.Abs(z - z1) < 1e-15)
                {
                    break;
                }
            }

            nodes[i] = -z;
            nodes[n - 1 - i] = z;

            double weight = 2.0 / (p2Final * p2Final);
            weights[i] = weight;
            weights[n - 1 - i] = weight;
        }

        return (nodes, weights);
    }
}
