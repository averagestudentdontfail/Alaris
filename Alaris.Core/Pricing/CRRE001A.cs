// CRRE001A.cs - Rate Regime Classifier
// Component ID: CRRE001A
//
// Classifies the interest rate regime for American option pricing.
// Based on Healy (2021) "American Options Under Negative Rates"
//
// Three regimes:
// 1. Standard (r ≥ 0 OR r < q < 0): Single boundary FD works
// 2. Double Boundary (q < r < 0 for puts, 0 < r < q for calls): Two exercise boundaries
// 3. The third case implicitly handled by the classification logic

namespace Alaris.Core.Pricing;

/// <summary>
/// Interest rate regime classification for American option pricing.
/// </summary>
public enum RateRegime
{
    /// <summary>
    /// Standard regime: r ≥ 0 or r < q < 0.
    /// Single exercise boundary. FD engine works correctly.
    /// </summary>
    Standard,

    /// <summary>
    /// Double boundary regime: q < r < 0 for puts, or 0 < r < q for calls.
    /// Two optimal exercise boundaries exist. Requires QD+ or FP-B' method.
    /// </summary>
    DoubleBoundary
}

/// <summary>
/// Rate regime classifier for American option pricing.
/// Determines whether single-boundary FD or double-boundary QD+ is required.
/// </summary>
/// <remarks>
/// Reference: Healy (2021) - American Options Under Negative Rates
/// 
/// Regime classification:
/// 
/// For PUT options:
///   - q < r < 0 → Double boundary (both upper and lower exercise boundaries)
///   - Otherwise → Standard single boundary
/// 
/// For CALL options:
///   - 0 < r < q → Double boundary (both upper and lower exercise boundaries)
///   - Otherwise → Standard single boundary
/// </remarks>
public static class CRRE001A
{
    /// <summary>
    /// Classifies the rate regime for pricing.
    /// </summary>
    /// <param name="r">Risk-free rate (can be negative).</param>
    /// <param name="q">Dividend yield (can be negative).</param>
    /// <param name="isCall">True for call options, false for puts.</param>
    /// <returns>The applicable rate regime.</returns>
    public static RateRegime Classify(double r, double q, bool isCall)
    {
        if (isCall)
        {
            // Call: Double boundary when 0 < r < q
            // This means dividend yield exceeds positive risk-free rate
            if (r > 0 && r < q)
            {
                return RateRegime.DoubleBoundary;
            }
        }
        else
        {
            // Put: Double boundary when q < r < 0
            // This means both rates negative, but dividend MORE negative than rate
            if (r < 0 && q < r)
            {
                return RateRegime.DoubleBoundary;
            }
        }

        return RateRegime.Standard;
    }

    /// <summary>
    /// Checks if the parameters require double-boundary treatment.
    /// </summary>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <param name="isCall">True for call options.</param>
    /// <returns>True if double-boundary method is required.</returns>
    public static bool RequiresDoubleBoundary(double r, double q, bool isCall)
    {
        return Classify(r, q, isCall) == RateRegime.DoubleBoundary;
    }

    /// <summary>
    /// Gets a human-readable description of the rate regime.
    /// </summary>
    /// <param name="regime">Rate regime to describe.</param>
    /// <returns>Description string.</returns>
    public static string GetDescription(RateRegime regime)
    {
        return regime switch
        {
            RateRegime.Standard => "Standard single-boundary (FD method applies)",
            RateRegime.DoubleBoundary => "Double-boundary (QD+ or FP-B' method required)",
            _ => "Unknown regime"
        };
    }

    /// <summary>
    /// Validates that parameters are within reasonable bounds.
    /// </summary>
    /// <param name="r">Risk-free rate.</param>
    /// <param name="q">Dividend yield.</param>
    /// <returns>True if parameters are valid.</returns>
    public static bool ValidateParameters(double r, double q)
    {
        // Check for NaN or infinity
        if (double.IsNaN(r) || double.IsInfinity(r))
        {
            return false;
        }

        if (double.IsNaN(q) || double.IsInfinity(q))
        {
            return false;
        }

        // Reasonable bounds for rates (within ±50%)
        const double MaxRate = 0.5;
        if (System.Math.Abs(r) > MaxRate || System.Math.Abs(q) > MaxRate)
        {
            return false;
        }

        return true;
    }
}
