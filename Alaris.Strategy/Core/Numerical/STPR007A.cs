// STPR007A.cs - Brent's Method Root Finder
// Component: STPR007A | Category: Numerical | Variant: A (Primary)
// =============================================================================
// Brent's method combines bisection safety with superlinear convergence.
// Replaces Newton-Bisection cascade in IV solvers.
// =============================================================================
// References:
// - Brent (1973) "Algorithms for Minimization Without Derivatives"
// - Press et al. (2007) "Numerical Recipes" Chapter 9.3
// =============================================================================

#pragma warning disable IDE0047 // Remove unnecessary parentheses
#pragma warning disable IDE0048 // Add parentheses for clarity

namespace Alaris.Strategy.Core.Numerical;

/// <summary>
/// Brent's method root-finding algorithm.
/// Combines bisection, secant, and inverse quadratic interpolation.
/// </summary>
/// <remarks>
/// Brent's method is the gold standard for derivative-free root finding:
/// - Guaranteed convergence (like bisection)
/// - Superlinear convergence for smooth functions
/// - No derivative required (unlike Newton-Raphson)
/// - Self-adaptive: switches between methods automatically
/// </remarks>
public static class STPR007A
{
    private const int DefaultMaxIterations = 100;
    private const double DefaultTolerance = 1e-10;
    private const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>
    /// Finds a root of f(x) = 0 in the interval [a, b] using Brent's method.
    /// </summary>
    /// <param name="f">Function whose root is sought.</param>
    /// <param name="a">Lower bound of bracket.</param>
    /// <param name="b">Upper bound of bracket.</param>
    /// <param name="tolerance">Convergence tolerance.</param>
    /// <param name="maxIterations">Maximum iterations.</param>
    /// <returns>Root x such that f(x) ≈ 0.</returns>
    /// <exception cref="ArgumentException">If f(a) and f(b) have same sign.</exception>
    public static double FindRoot(
        Func<double, double> f,
        double a,
        double b,
        double tolerance = DefaultTolerance,
        int maxIterations = DefaultMaxIterations)
    {
        ArgumentNullException.ThrowIfNull(f);

        double fa = f(a);
        double fb = f(b);

        // Check bracketing condition
        if (fa * fb > 0)
        {
            throw new ArgumentException(
                $"Root not bracketed: f({a})={fa:E4}, f({b})={fb:E4} have same sign.");
        }

        // Ensure |f(a)| >= |f(b)| for algorithm stability
        if (Math.Abs(fa) < Math.Abs(fb))
        {
            (a, b) = (b, a);
            (fa, fb) = (fb, fa);
        }

        double c = a;
        double fc = fa;
        bool mflag = true;
        double d = 0; // Previous step, only valid when mflag is false

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // Converged?
            if (Math.Abs(fb) < tolerance || Math.Abs(b - a) < tolerance)
            {
                return b;
            }

            double s;
            if (Math.Abs(fa - fc) > MachineEpsilon &&
                Math.Abs(fb - fc) > MachineEpsilon)
            {
                // Inverse quadratic interpolation
                s = a * fb * fc / ((fa - fb) * (fa - fc)) +
                    b * fa * fc / ((fb - fa) * (fb - fc)) +
                    c * fa * fb / ((fc - fa) * (fc - fb));
            }
            else
            {
                // Secant method
                s = b - (fb * (b - a) / (fb - fa));
            }

            // Conditions for using bisection instead
            double temp1 = ((3 * a) + b) / 4;
            bool condition1 = !((s > temp1 && s < b) || (s > b && s < temp1));
            bool condition2 = mflag && Math.Abs(s - b) >= Math.Abs(b - c) / 2;
            bool condition3 = !mflag && Math.Abs(s - b) >= Math.Abs(c - d) / 2;
            bool condition4 = mflag && Math.Abs(b - c) < tolerance;
            bool condition5 = !mflag && Math.Abs(c - d) < tolerance;

            if (condition1 || condition2 || condition3 || condition4 || condition5)
            {
                // Bisection
                s = (a + b) / 2;
                mflag = true;
            }
            else
            {
                mflag = false;
            }

            double fs = f(s);
            d = c;
            c = b;
            fc = fb;

            if (fa * fs < 0)
            {
                b = s;
                fb = fs;
            }
            else
            {
                a = s;
                fa = fs;
            }

            // Ensure |f(a)| >= |f(b)|
            if (Math.Abs(fa) < Math.Abs(fb))
            {
                (a, b) = (b, a);
                (fa, fb) = (fb, fa);
            }
        }

        // Return best approximation
        return b;
    }

    /// <summary>
    /// Result of root-finding operation.
    /// </summary>
    public readonly record struct RootResult(
        double Root,
        double FunctionValue,
        int Iterations,
        bool Converged);

    /// <summary>
    /// Finds root with detailed result including convergence info.
    /// </summary>
    public static RootResult FindRootWithInfo(
        Func<double, double> f,
        double a,
        double b,
        double tolerance = DefaultTolerance,
        int maxIterations = DefaultMaxIterations)
    {
        ArgumentNullException.ThrowIfNull(f);

        double fa = f(a);
        double fb = f(b);

        if (fa * fb > 0)
        {
            return new RootResult(double.NaN, double.NaN, 0, false);
        }

        if (Math.Abs(fa) < Math.Abs(fb))
        {
            (a, b) = (b, a);
            (fa, fb) = (fb, fa);
        }

        double c = a;
        double fc = fa;
        bool mflag = true;
        double d = 0;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            if (Math.Abs(fb) < tolerance || Math.Abs(b - a) < tolerance)
            {
                return new RootResult(b, fb, iter + 1, true);
            }

            double s;
            if (Math.Abs(fa - fc) > MachineEpsilon &&
                Math.Abs(fb - fc) > MachineEpsilon)
            {
            s = a * fb * fc / ((fa - fb) * (fa - fc)) +
                    b * fa * fc / ((fb - fa) * (fb - fc)) +
                    c * fa * fb / ((fc - fa) * (fc - fb));
            }
            else
            {
                s = b - (fb * (b - a) / (fb - fa));
            }

            double temp1 = ((3 * a) + b) / 4;
            bool condition1 = !((s > temp1 && s < b) || (s > b && s < temp1));
            bool condition2 = mflag && Math.Abs(s - b) >= Math.Abs(b - c) / 2;
            bool condition3 = !mflag && Math.Abs(s - b) >= Math.Abs(c - d) / 2;
            bool condition4 = mflag && Math.Abs(b - c) < tolerance;
            bool condition5 = !mflag && Math.Abs(c - d) < tolerance;

            if (condition1 || condition2 || condition3 || condition4 || condition5)
            {
                s = (a + b) / 2;
                mflag = true;
            }
            else
            {
                mflag = false;
            }

            double fs = f(s);
            d = c;
            c = b;
            fc = fb;

            if (fa * fs < 0)
            {
                b = s;
                fb = fs;
            }
            else
            {
                a = s;
                fa = fs;
            }

            if (Math.Abs(fa) < Math.Abs(fb))
            {
                (a, b) = (b, a);
                (fa, fb) = (fb, fa);
            }
        }

        return new RootResult(b, fb, maxIterations, false);
    }

    /// <summary>
    /// Solves for implied volatility given a pricing function.
    /// Specialized convenience method for IV solving.
    /// </summary>
    /// <param name="priceFunction">BS price as function of volatility.</param>
    /// <param name="targetPrice">Target option price.</param>
    /// <param name="minVol">Minimum volatility bound.</param>
    /// <param name="maxVol">Maximum volatility bound.</param>
    /// <param name="tolerance">Convergence tolerance.</param>
    /// <returns>Implied volatility.</returns>
    public static double SolveImpliedVolatility(
        Func<double, double> priceFunction,
        double targetPrice,
        double minVol = 0.001,
        double maxVol = 5.0,
        double tolerance = 1e-8)
    {
        return FindRoot(
            vol => priceFunction(vol) - targetPrice,
            minVol,
            maxVol,
            tolerance);
    }
}
