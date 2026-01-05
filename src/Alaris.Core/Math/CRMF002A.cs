// CRMF002A.cs - Characteristic Equation Solver
// Component ID: CRMF002A
//
// Solves the Black-Scholes characteristic equation:
// (1/2)σ²λ² + (r - q - σ²/2)λ - r = 0
//
// Under positive rates (r > 0): λ₁ > 1, λ₂ < 0
// Under negative rates (q < r < 0): λ₁ ∈ (0,1), λ₂ < 0
//
// References:
// - Healy (2021) "American Options Under Negative Rates"
// - Kim (1990) "Analytic Valuation of American Options"

using System;
using System.Diagnostics.CodeAnalysis;

namespace Alaris.Core.Math;

/// <summary>
/// Characteristic equation solver for American option pricing.
/// Computes λ₁ and λ₂ roots used in boundary calculations.
/// </summary>
public static class CRMF002A
{
    /// <summary>
    /// Solves the characteristic equation for the given parameters.
    /// </summary>
    /// <param name="r">Risk-free rate (can be negative).</param>
    /// <param name="q">Dividend yield (can be negative).</param>
    /// <param name="σ">Volatility (must be positive).</param>
    /// <returns>Tuple of (λ₁, λ₂) roots, where λ₁ > λ₂.</returns>
    /// <remarks>
    /// Characteristic equation: (1/2)σ²λ² + (r - q - σ²/2)λ - r = 0
    /// 
    /// Coefficients: a = σ²/2, b = r - q - σ²/2, c = -r
    /// </remarks>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:VariableNamesMustBeginWithLowerCaseLetter", Justification = "Greek letters")]
    public static (double λ1, double λ2) SolveCharacteristic(double r, double q, double σ)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero(σ, nameof(σ));

        double σ2 = σ * σ;
        
        // Characteristic equation coefficients
        // aλ² + bλ + c = 0
        double a = σ2 / 2.0;
        double b = r - q - (σ2 / 2.0);
        double c = -r;

        // Quadratic formula: λ = (-b ± √(b² - 4ac)) / 2a
        double discriminant = (b * b) - (4.0 * a * c);
        
        if (discriminant < 0)
        {
            // Should not happen for valid parameters, but handle gracefully
            throw new ArgumentException("Complex roots detected for the provided parameter combination.");
        }

        double sqrtD = System.Math.Sqrt(discriminant);
        
        // Initial roots from quadratic formula
        double λ1Init = (-b + sqrtD) / (2.0 * a);
        double λ2Init = (-b - sqrtD) / (2.0 * a);

        // Refine using Super-Halley for numerical stability near r ≈ 0
        double λ1 = SuperHalleyRefine(λ1Init, a, b, c);
        double λ2 = SuperHalleyRefine(λ2Init, a, b, c);

        // Ensure λ1 > λ2 (convention)
        if (λ1 < λ2)
        {
            (λ1, λ2) = (λ2, λ1);
        }

        return (λ1, λ2);
    }

    /// <summary>
    /// Refines a characteristic root using Super-Halley iteration.
    /// </summary>
    /// <param name="λInit">Initial root estimate.</param>
    /// <param name="a">Quadratic coefficient (σ²/2).</param>
    /// <param name="b">Linear coefficient (r - q - σ²/2).</param>
    /// <param name="c">Constant coefficient (-r).</param>
    /// <param name="tolerance">Convergence tolerance.</param>
    /// <param name="maxIterations">Maximum iterations.</param>
    /// <returns>Refined root value.</returns>
    /// <remarks>
    /// Super-Halley iteration formula:
    /// λₙ₊₁ = λₙ - (f(λₙ)·f'(λₙ)) / (f'(λₙ)² - ½f(λₙ)·f''(λₙ))
    /// 
    /// For quadratic: f(λ) = aλ² + bλ + c
    ///                f'(λ) = 2aλ + b
    ///                f''(λ) = 2a
    /// </remarks>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:VariableNamesMustBeginWithLowerCaseLetter", Justification = "Greek letters")]
    public static double SuperHalleyRefine(
        double λInit, 
        double a, 
        double b, 
        double c, 
        double tolerance = 1e-12, 
        int maxIterations = 20)
    {
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxIterations, 1);
        double λ = λInit;

        for (int i = 0; i < maxIterations; i++)
        {
            // f(λ) = aλ² + bλ + c
            double f = (a * λ * λ) + (b * λ) + c;

            // Check convergence
            if (System.Math.Abs(f) < tolerance)
            {
                return λ;
            }

            // f'(λ) = 2aλ + b
            double fPrime = (2.0 * a * λ) + b;

            // f''(λ) = 2a
            double fDoublePrime = 2.0 * a;

            // Super-Halley denominator
            double denominator = (fPrime * fPrime) - (0.5 * f * fDoublePrime);

            // Guard against division by near-zero - use Brent bisection fallback
            if (System.Math.Abs(denominator) < 1e-15 || System.Math.Abs(fPrime) < 1e-15)
            {
                // Brent bisection: guaranteed convergence
                return BrentBisectionFallback(λ, a, b, c, tolerance);
            }

            // Super-Halley update
            double λNext = λ - ((f * fPrime) / denominator);

            // Check for convergence
            if (System.Math.Abs(λNext - λ) < tolerance)
            {
                return λNext;
            }

            λ = λNext;
        }

        return λ;
    }

    /// <summary>
    /// Brent bisection fallback for quadratic root finding - guaranteed convergence.
    /// </summary>
    /// <param name="λInit">Initial estimate.</param>
    /// <param name="a">Quadratic coefficient.</param>
    /// <param name="b">Linear coefficient.</param>
    /// <param name="c">Constant coefficient.</param>
    /// <param name="tolerance">Convergence tolerance.</param>
    /// <returns>Refined root value.</returns>
    /// <remarks>
    /// Uses bisection with bracketing. For a quadratic with real roots,
    /// we can bracket by expanding around the initial estimate.
    /// </remarks>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:VariableNamesMustBeginWithLowerCaseLetter", Justification = "Greek letters")]
    private static double BrentBisectionFallback(
        double λInit,
        double a,
        double b,
        double c,
        double tolerance = 1e-12)
    {
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
        }

        // Evaluate f(λ) = aλ² + bλ + c
        static double EvalQuadratic(double λ, double a, double b, double c)
        {
            return (a * λ * λ) + (b * λ) + c;
        }

        // Find bracketing interval around initial estimate
        double lower = λInit - System.Math.Abs(λInit) - 10.0;
        double upper = λInit + System.Math.Abs(λInit) + 10.0;

        double fLower = EvalQuadratic(lower, a, b, c);
        double fUpper = EvalQuadratic(upper, a, b, c);

        // Ensure we have a sign change (root is bracketed)
        // If not, expand the interval
        for (int expand = 0; expand < 10 && fLower * fUpper > 0; expand++)
        {
            lower -= 10.0;
            upper += 10.0;
            fLower = EvalQuadratic(lower, a, b, c);
            fUpper = EvalQuadratic(upper, a, b, c);
        }

        // If still not bracketed, return initial estimate (shouldn't happen for valid quadratics)
        if (fLower * fUpper > 0)
        {
            return λInit;
        }

        // Bisection iteration (guaranteed convergence)
        for (int i = 0; i < 100; i++)
        {
            double mid = (lower + upper) / 2.0;
            double fMid = EvalQuadratic(mid, a, b, c);

            if (System.Math.Abs(fMid) < tolerance || (upper - lower) / 2.0 < tolerance)
            {
                return mid;
            }

            if (fLower * fMid < 0)
            {
                upper = mid;
                fUpper = fMid;
            }
            else
            {
                lower = mid;
                fLower = fMid;
            }
        }

        return (lower + upper) / 2.0;
    }

    /// <summary>
    /// Validates that the computed roots satisfy the characteristic equation.
    /// </summary>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="σ">Volatility.</param>
    /// <param name="λ">Root to validate.</param>
    /// <param name="tolerance">Maximum acceptable residual.</param>
    /// <returns>True if the root satisfies the equation within tolerance.</returns>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1312:VariableNamesMustBeginWithLowerCaseLetter", Justification = "Greek letters")]
    public static bool ValidateRoot(double r, double q, double σ, double λ, double tolerance = 1e-10)
    {
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
        }

        double σ2 = σ * σ;
        
        // f(λ) = (σ²/2)λ² + (r - q - σ²/2)λ - r
        double residual = (σ2 / 2.0 * λ * λ) + ((r - q - σ2 / 2.0) * λ) - r;
        
        return System.Math.Abs(residual) < tolerance;
    }
}
