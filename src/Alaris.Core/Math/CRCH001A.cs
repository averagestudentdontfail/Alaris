// CRCH001A.cs - Chebyshev Polynomial Infrastructure
// Component ID: CRCH001A
//
// Provides Chebyshev nodes, barycentric interpolation, and polynomial evaluation
// for spectral collocation methods in American option pricing.
//
// References:
// - Berrut & Trefethen (2004) "Barycentric Lagrange Interpolation"
// - Andersen, Lake & Offengenden (2016) "High Performance American Option Pricing"

using System;
using System.Runtime.CompilerServices;

namespace Alaris.Core.Math;

/// <summary>
/// Chebyshev polynomial infrastructure for spectral collocation methods.
/// </summary>
public static class CRCH001A
{
    private const double NodeMatchTolerance = 1e-14;

    /// <summary>
    /// Generates Chebyshev nodes of the first kind on interval [a, b].
    /// </summary>
    /// <param name="n">Number of nodes (must be >= 1).</param>
    /// <param name="a">Left endpoint of interval.</param>
    /// <param name="b">Right endpoint of interval.</param>
    /// <returns>Array of n Chebyshev nodes in ascending order.</returns>
    /// <remarks>
    /// Chebyshev nodes: x_k = (a+b)/2 + (b-a)/2 * cos((2k+1)π/(2n)) for k = 0,...,n-1
    /// These nodes minimize the Lebesgue constant and avoid Runge's phenomenon.
    /// </remarks>
    public static double[] ChebyshevNodes(int n, double a = 0.0, double b = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);

        if (b <= a)
        {
            throw new ArgumentOutOfRangeException(nameof(b), "Right endpoint must be greater than left endpoint.");
        }

        double[] nodes = new double[n];
        double mid = (a + b) / 2.0;
        double halfWidth = (b - a) / 2.0;

        for (int k = 0; k < n; k++)
        {
            double theta = (2.0 * k + 1.0) * System.Math.PI / (2.0 * n);
            nodes[k] = mid + (halfWidth * System.Math.Cos(theta));
        }

        // Sort in ascending order (cos gives descending)
        Array.Reverse(nodes);

        return nodes;
    }

    /// <summary>
    /// Generates Chebyshev-Lobatto nodes (extrema of T_n) including endpoints.
    /// </summary>
    /// <param name="n">Number of nodes (must be >= 2).</param>
    /// <param name="a">Left endpoint of interval.</param>
    /// <param name="b">Right endpoint of interval.</param>
    /// <returns>Array of n Chebyshev-Lobatto nodes including a and b.</returns>
    /// <remarks>
    /// Chebyshev-Lobatto nodes: x_k = (a+b)/2 + (b-a)/2 * cos(kπ/(n-1)) for k = 0,...,n-1
    /// These include the endpoints [a, b] which is useful for boundary value problems.
    /// </remarks>
    public static double[] ChebyshevLobattoNodes(int n, double a = 0.0, double b = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 2);

        if (b <= a)
        {
            throw new ArgumentOutOfRangeException(nameof(b), "Right endpoint must be greater than left endpoint.");
        }

        double[] nodes = new double[n];
        double mid = (a + b) / 2.0;
        double halfWidth = (b - a) / 2.0;

        for (int k = 0; k < n; k++)
        {
            double theta = k * System.Math.PI / (n - 1);
            nodes[k] = mid + (halfWidth * System.Math.Cos(theta));
        }

        // Sort in ascending order
        Array.Reverse(nodes);

        return nodes;
    }

    /// <summary>
    /// Evaluates the Chebyshev polynomial T_n(x) using the recurrence relation.
    /// </summary>
    /// <param name="n">Polynomial degree.</param>
    /// <param name="x">Evaluation point in [-1, 1].</param>
    /// <returns>T_n(x).</returns>
    /// <remarks>
    /// Recurrence: T_0(x) = 1, T_1(x) = x, T_{n+1}(x) = 2x*T_n(x) - T_{n-1}(x)
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ChebyshevT(int n, double x)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);

        if (n == 0)
        {
            return 1.0;
        }

        if (n == 1)
        {
            return x;
        }

        double tPrev = 1.0;  // T_0
        double tCurr = x;     // T_1

        for (int k = 2; k <= n; k++)
        {
            double tNext = (2.0 * x * tCurr) - tPrev;
            tPrev = tCurr;
            tCurr = tNext;
        }

        return tCurr;
    }

    /// <summary>
    /// Computes barycentric interpolation weights for Chebyshev nodes.
    /// </summary>
    /// <param name="n">Number of nodes.</param>
    /// <returns>Array of barycentric weights.</returns>
    /// <remarks>
    /// For Chebyshev nodes, weights are: w_k = (-1)^k * sin((2k+1)π/(2n))
    /// Simplified form: w_k = (-1)^k with scaling (all have same magnitude).
    /// </remarks>
    public static double[] BarycentricWeights(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 1);

        double[] weights = new double[n];

        for (int k = 0; k < n; k++)
        {
            // Weights alternate in sign
            weights[k] = (k % 2 == 0) ? 1.0 : -1.0;

            // Apply sin factor for improved numerical stability
            double theta = (2.0 * k + 1.0) * System.Math.PI / (2.0 * n);
            weights[k] *= System.Math.Sin(theta);
        }

        return weights;
    }

    /// <summary>
    /// Computes barycentric weights for Chebyshev-Lobatto nodes.
    /// </summary>
    /// <param name="n">Number of nodes.</param>
    /// <returns>Array of barycentric weights for Lobatto nodes.</returns>
    /// <remarks>
    /// For Chebyshev-Lobatto nodes: w_k = (-1)^k * δ_k
    /// where δ_0 = δ_{n-1} = 1/2, δ_k = 1 otherwise.
    /// </remarks>
    public static double[] BarycentricWeightsLobatto(int n)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, 2);

        double[] weights = new double[n];

        for (int k = 0; k < n; k++)
        {
            double delta = (k == 0 || k == n - 1) ? 0.5 : 1.0;
            weights[k] = ((k % 2 == 0) ? 1.0 : -1.0) * delta;
        }

        return weights;
    }

    /// <summary>
    /// Interpolates a function at an arbitrary point using barycentric formula.
    /// </summary>
    /// <param name="nodes">Interpolation nodes (in ascending order).</param>
    /// <param name="values">Function values at nodes.</param>
    /// <param name="weights">Barycentric weights.</param>
    /// <param name="x">Evaluation point.</param>
    /// <returns>Interpolated value at x.</returns>
    /// <remarks>
    /// Barycentric formula: p(x) = Σ(w_k * f_k / (x - x_k)) / Σ(w_k / (x - x_k))
    /// This is O(n) per evaluation and numerically stable.
    /// </remarks>
    public static double Interpolate(double[] nodes, double[] values, double[] weights, double x)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(weights);

        int n = nodes.Length;
        if (n == 0)
        {
            throw new ArgumentException("Nodes must not be empty.", nameof(nodes));
        }

        if (values.Length != n || weights.Length != n)
        {
            throw new ArgumentException("Nodes, values, and weights must have the same length.");
        }

        for (int k = 0; k < n; k++)
        {
            double diff = x - nodes[k];
            if (System.Math.Abs(diff) < NodeMatchTolerance)
            {
                return values[k];
            }
        }

        double numerator = 0.0;
        double denominator = 0.0;

        for (int k = 0; k < n; k++)
        {
            double term = weights[k] / (x - nodes[k]);
            numerator += term * values[k];
            denominator += term;
        }

        return numerator / denominator;
    }

    /// <summary>
    /// Interpolates using pre-computed nodes and weights (convenience overload).
    /// </summary>
    public static double Interpolate(double[] nodes, double[] values, double x)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        double[] weights = BarycentricWeights(nodes.Length);
        return Interpolate(nodes, values, weights, x);
    }

    /// <summary>
    /// Transforms a point from physical domain [a, b] to standard Chebyshev domain [-1, 1].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToStandardDomain(double x, double a, double b)
    {
        if (b <= a)
        {
            throw new ArgumentOutOfRangeException(nameof(b), "Right endpoint must be greater than left endpoint.");
        }

        return (2.0 * x - a - b) / (b - a);
    }

    /// <summary>
    /// Transforms a point from standard Chebyshev domain [-1, 1] to physical domain [a, b].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FromStandardDomain(double t, double a, double b)
    {
        if (b <= a)
        {
            throw new ArgumentOutOfRangeException(nameof(b), "Right endpoint must be greater than left endpoint.");
        }

        return ((b - a) * t + a + b) / 2.0;
    }

    /// <summary>
    /// Computes the Chebyshev differentiation matrix for spectral differentiation.
    /// </summary>
    /// <param name="nodes">Chebyshev-Lobatto nodes in [-1, 1].</param>
    /// <returns>n×n differentiation matrix D where (Df)_i ≈ f'(x_i).</returns>
    /// <remarks>
    /// The differentiation matrix maps function values to approximate derivative values.
    /// This is useful for solving ODEs/PDEs spectrally.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1814:Prefer jagged arrays over multidimensional", Justification = "Matrix representation requires multidimensional array")]
    public static double[,] DifferentiationMatrix(double[] nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        int n = nodes.Length;
        if (n < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(nodes), "At least two nodes are required.");
        }
        double[,] D = new double[n, n];

        double[] c = new double[n];
        for (int k = 0; k < n; k++)
        {
            c[k] = (k == 0 || k == n - 1) ? 2.0 : 1.0;
            c[k] *= (k % 2 == 0) ? 1.0 : -1.0;
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i != j)
                {
                    D[i, j] = c[i] / (c[j] * (nodes[i] - nodes[j]));
                }
            }
        }

        for (int i = 0; i < n; i++)
        {
            double rowSum = 0.0;
            for (int j = 0; j < n; j++)
            {
                if (i != j)
                {
                    rowSum += D[i, j];
                }
            }
            D[i, i] = -rowSum;
        }

        return D;
    }

    /// <summary>
    /// Computes Chebyshev coefficients from function values (DCT-I on Lobatto nodes).
    /// </summary>
    /// <param name="values">Function values at Chebyshev-Lobatto nodes.</param>
    /// <returns>Chebyshev coefficients a_k for f(x) ≈ 0.5a_0 + Σ a_k T_k(x) + 0.5a_n T_n(x).</returns>
    public static double[] ValuesToCoefficients(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int n = values.Length;
        if (n == 0)
        {
            throw new ArgumentException("Values must not be empty.", nameof(values));
        }

        if (n == 1)
        {
            return new[] { values[0] };
        }

        double[] coeffs = new double[n];

        for (int k = 0; k < n; k++)
        {
            double sum = 0.0;
            for (int j = 0; j < n; j++)
            {
                double theta = j * System.Math.PI / (n - 1);
                double weight = (j == 0 || j == n - 1) ? 0.5 : 1.0;
                sum += weight * values[j] * System.Math.Cos(k * theta);
            }

            coeffs[k] = (2.0 / (n - 1)) * sum;
        }

        return coeffs;
    }

    /// <summary>
    /// Evaluates a Chebyshev series at a point using Clenshaw's algorithm.
    /// </summary>
    /// <param name="coeffs">Chebyshev coefficients with half-weighted endpoints.</param>
    /// <param name="x">Evaluation point in [-1, 1].</param>
    /// <returns>Value of the Chebyshev series at x.</returns>
    /// <remarks>
    /// Clenshaw's algorithm is numerically stable and O(n).
    /// </remarks>
    public static double EvaluateSeries(double[] coeffs, double x)
    {
        ArgumentNullException.ThrowIfNull(coeffs);
        int n = coeffs.Length;
        if (n == 0)
        {
            return 0.0;
        }

        if (n == 1)
        {
            return coeffs[0];
        }

        double bk1 = 0.0;
        double bk2 = 0.0;

        for (int k = n - 1; k >= 1; k--)
        {
            double coeff = k == n - 1 ? 0.5 * coeffs[k] : coeffs[k];
            double bk = coeff + (2.0 * x * bk1) - bk2;
            bk2 = bk1;
            bk1 = bk;
        }

        return (0.5 * coeffs[0]) + (x * bk1) - bk2;
    }
}
