using System;
using Alaris.Core.Validation;

namespace Alaris.Double;

/// <summary>
/// Complete solver for American options with double boundaries under negative rates.
/// Combines QD+ approximation with FP-B' stabilized Kim integral equation refinement.
/// </summary>
/// <remarks>
/// <para>
/// Two-stage solving process:
/// 1. QD+ approximation with Super Halley's method provides fast initial boundaries
/// 2. FP-B' stabilized Kim solver refines using fixed point iteration
/// </para>
/// <para>
/// Architecture mirrors QuantLib's approach:
/// - Single boundary (r ≥ 0): QdPlus → QdFp (Chebyshev)
/// - Double boundary (q &lt; r &lt; 0): QdPlus → FP-B' Kim (collocation + stabilized fixed point)
/// </para>
/// <para>
/// Key improvement: Uses FP-B' (Healy Equations 33-35) instead of basic FP-B to prevent
/// oscillations in longer maturity options.
/// </para>
/// </remarks>
public sealed class DBSL001A
{
    private readonly double _spot;
    private readonly double _strike;
    private readonly double _maturity;
    private readonly double _rate;
    private readonly double _dividendYield;
    private readonly double _volatility;
    private readonly bool _isCall;
    private readonly int _collocationPoints;
    private readonly bool _useRefinement;
    
    /// <summary>
    /// Initializes the double boundary solver.
    /// </summary>
    /// <param name="spot">Current asset price</param>
    /// <param name="strike">Strike price</param>
    /// <param name="maturity">Time to maturity (years)</param>
    /// <param name="rate">Risk-free rate (negative for negative rate regime)</param>
    /// <param name="dividendYield">Dividend yield (negative for negative rate regime)</param>
    /// <param name="volatility">Volatility</param>
    /// <param name="isCall">True for call, false for put</param>
    /// <param name="collocationPoints">Number of time points (default 50)</param>
    /// <param name="useRefinement">Use FP-B' Kim refinement (default true)</param>
    /// <exception cref="BoundsViolationException">Thrown if inputs violate algorithm bounds.</exception>
    public DBSL001A(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall,
        int collocationPoints = 50,
        bool useRefinement = true)
    {
        // Standardised bounds validation (Rule 9)
        AlgorithmBounds.ValidateDoubleBoundaryInputs(
            spot, strike, maturity, rate, dividendYield, volatility);

        if (collocationPoints <= 0)
        {
            throw new ArgumentException("Collocation points must be positive", nameof(collocationPoints));
        }

        _spot = spot;
        _strike = strike;
        _maturity = maturity;
        _rate = rate;
        _dividendYield = dividendYield;
        _volatility = volatility;
        _isCall = isCall;
        _collocationPoints = collocationPoints;
        _useRefinement = useRefinement;
    }
    
    /// <summary>
    /// Solves for both boundaries using QD+ approximation and optional Kim refinement.
    /// </summary>
    /// <remarks>
    /// For very short maturities (T &lt; 3/252 years ≈ 3 trading days), the method 
    /// uses the near-expiry handler to avoid numerical singularities in the QD+ 
    /// asymptotic expansion.
    /// </remarks>
    /// <returns>Solution containing boundaries and metadata</returns>
    public DoubleBoundaryResult Solve()
    {
        // Near-expiry guard: Handle T near zero where QD+ has numerical issues
        DBEX001A nearExpiryHandler = new DBEX001A();
        NearExpiryValidation validation = nearExpiryHandler.Validate(_maturity);

        if (validation.Recommendation == NearExpiryRecommendation.UseIntrinsic ||
            validation.Recommendation == NearExpiryRecommendation.UseBlended)
        {
            // Very near expiry or in blending zone: return intrinsic-based boundaries
            // QD+ asymptotic expansion is numerically unstable in this regime
            return CreateNearExpiryResult(validation);
        }

        // Stage 1: QD+ approximation for initial boundaries
        (double upperInitial, double lowerInitial) = CalculateInitialBoundaries();

        // Check regime and refinement settings
        bool isDoubleBoundary = DetectDoubleBoundaryRegime();

        if (!isDoubleBoundary)
        {
            return CreateSingleBoundaryResult(upperInitial, lowerInitial);
        }

        if (!_useRefinement)
        {
            return CreateQdOnlyResult(upperInitial, lowerInitial);
        }

        // Stage 2: Kim refinement with FP-B' stabilization
        return ApplyKimRefinement(upperInitial, lowerInitial);
    }

    /// <summary>
    /// Creates result for near-expiry regime where QD+ is numerically unstable.
    /// </summary>
    /// <remarks>
    /// Near expiry, boundaries collapse towards strike and intrinsic value dominates.
    /// Uses limiting behavior: S_u → K, S_l → K (boundaries merge at strike).
    /// Spread scales with σ√T for dimensional correctness.
    /// </remarks>
    private DoubleBoundaryResult CreateNearExpiryResult(NearExpiryValidation validation)
    {
        // Near expiry boundary spread should scale with σ√T (diffusion theory)
        // This ensures dimensional correctness: spread ~ σ√T, not fixed percentage
        double theoreticalSpread = Math.Max(0.01, _volatility * Math.Sqrt(_maturity));

        // For puts: exercise optimal when S < K
        // Upper boundary: slightly below strike (0.3× spread factor)
        // Lower boundary: further below strike (full spread factor)
        double nearExpiryUpper = _isCall
            ? _strike * (1.0 + (0.3 * theoreticalSpread))
            : _strike * (1.0 - (0.3 * theoreticalSpread));

        double nearExpiryLower = _isCall
            ? _strike * (1.0 + (0.1 * theoreticalSpread))
            : _strike * (1.0 - theoreticalSpread);

        // Ensure ordering constraints (A2: upper > lower)
        if (!_isCall && nearExpiryLower >= nearExpiryUpper)
        {
            nearExpiryLower = nearExpiryUpper * 0.99;
        }

        return new DoubleBoundaryResult
        {
            UpperBoundary = nearExpiryUpper,
            LowerBoundary = nearExpiryLower,
            QdUpperBoundary = nearExpiryUpper,
            QdLowerBoundary = nearExpiryLower,
            CrossingTime = 0.0,
            IsRefined = false,
            Method = $"Near-Expiry Handler ({validation.Reason})",
            IsValid = true,
            Iterations = 0
        };
    }

    /// <summary>
    /// Calculates initial boundary estimates using QD+ approximation.
    /// </summary>
    private (double upperInitial, double lowerInitial) CalculateInitialBoundaries()
    {
        DBAP001A qdplus = new DBAP001A(
            _spot, _strike, _maturity, _rate,
            _dividendYield, _volatility, _isCall);

        return qdplus.CalculateBoundaries();
    }

    /// <summary>
    /// Creates result for single boundary regime.
    /// </summary>
    private static DoubleBoundaryResult CreateSingleBoundaryResult(double upperInitial, double lowerInitial)
    {
        return new DoubleBoundaryResult
        {
            UpperBoundary = upperInitial,
            LowerBoundary = lowerInitial,
            QdUpperBoundary = upperInitial,
            QdLowerBoundary = lowerInitial,
            CrossingTime = 0.0,
            IsRefined = false,
            Method = "QD+ (Single Boundary)",
            IsValid = true,
            Iterations = 0
        };
    }

    /// <summary>
    /// Creates result using only QD+ approximation without refinement.
    /// </summary>
    private DoubleBoundaryResult CreateQdOnlyResult(double upperInitial, double lowerInitial)
    {
        return new DoubleBoundaryResult
        {
            UpperBoundary = upperInitial,
            LowerBoundary = lowerInitial,
            QdUpperBoundary = upperInitial,
            QdLowerBoundary = lowerInitial,
            CrossingTime = 0.0,
            IsRefined = false,
            Method = "QD+ Approximation",
            IsValid = ValidateBoundaries(upperInitial, lowerInitial),
            Iterations = 0
        };
    }

    /// <summary>
    /// Applies Kim refinement to initial QD+ boundaries.
    /// </summary>
    private DoubleBoundaryResult ApplyKimRefinement(double upperInitial, double lowerInitial)
    {
        DBSL002A kimSolver = new DBSL002A(
            _spot, _strike, _maturity, _rate,
            _dividendYield, _volatility, _isCall,
            _collocationPoints);

        (double[] upperRefined, double[] lowerRefined, double crossingTime) = kimSolver.SolveBoundaries(
            upperInitial, lowerInitial);

        // Extract boundary values at maturity
        int lastIndex = upperRefined.Length - 1;
        double upperFinal = upperRefined[lastIndex];
        double lowerFinal = lowerRefined[lastIndex];

        return new DoubleBoundaryResult
        {
            UpperBoundary = upperFinal,
            LowerBoundary = lowerFinal,
            QdUpperBoundary = upperInitial,
            QdLowerBoundary = lowerInitial,
            CrossingTime = crossingTime,
            IsRefined = true,
            Method = "QD+ + FP-B' Kim Refinement",
            IsValid = ValidateBoundaries(upperFinal, lowerFinal),
            Iterations = _collocationPoints,
            UpperBoundaryPath = upperRefined,
            LowerBoundaryPath = lowerRefined
        };
    }
    
    /// <summary>
    /// Detects if the option is in a double boundary regime.
    /// </summary>
    /// <remarks>
    /// Uses hysteresis bands to prevent oscillation when rates hover near zero.
    /// The regime only changes when rates clearly cross thresholds by at least
    /// the hysteresis epsilon (5 basis points).
    /// </remarks>
    private bool DetectDoubleBoundaryRegime()
    {
        // Hysteresis epsilon: 5 basis points prevents oscillation at regime boundaries
        const double HysteresisEpsilon = 0.0005;
        
        if (!_isCall)
        {
            // Put: double boundary when q < r < 0
            // With hysteresis: require clear crossing of thresholds
            bool rateClearlyNegative = _rate < -HysteresisEpsilon;
            bool dividendClearlyBelowRate = _dividendYield < _rate - HysteresisEpsilon;
            return dividendClearlyBelowRate && rateClearlyNegative;
        }
        else
        {
            // Call: double boundary when 0 < r < q
            // With hysteresis: require clear crossing of thresholds
            bool rateClearlyPositive = _rate > HysteresisEpsilon;
            bool rateClearlyBelowDividend = _rate < _dividendYield - HysteresisEpsilon;
            return rateClearlyPositive && rateClearlyBelowDividend;
        }
    }
    
    /// <summary>
    /// Validates boundary values for consistency.
    /// </summary>
    private bool ValidateBoundaries(double upper, double lower)
    {
        if (double.IsNaN(upper) || double.IsNaN(lower))
        {
            return false;
        }

        if (double.IsInfinity(upper) && double.IsInfinity(lower))
        {
            return false;
        }
        
        if (!_isCall)
        {
            // Put validation
            if (!double.IsPositiveInfinity(upper) && upper > _strike)
            {
                return false;
            }

            if (!double.IsNegativeInfinity(lower) && lower < 0)
            {
                return false;
            }

            if (!double.IsInfinity(upper) && !double.IsInfinity(lower) && lower >= upper)
            {
                return false;
            }
        }
        else
        {
            // Call validation
            if (!double.IsPositiveInfinity(upper) && upper < _strike)
            {
                return false;
            }

            if (!double.IsNegativeInfinity(lower) && lower < 0)
            {
                return false;
            }

            if (!double.IsInfinity(upper) && !double.IsInfinity(lower) && lower >= upper)
            {
                return false;
            }
        }
        
        return true;
    }
}

/// <summary>
/// Result from double boundary solver.
/// </summary>
public sealed class DoubleBoundaryResult
{
    /// <summary>
    /// Upper exercise boundary at maturity.
    /// </summary>
    public double UpperBoundary { get; init; }

    /// <summary>
    /// Lower exercise boundary at maturity.
    /// </summary>
    public double LowerBoundary { get; init; }

    /// <summary>
    /// Time when boundaries cross (0 if no crossing).
    /// </summary>
    public double CrossingTime { get; init; }

    /// <summary>
    /// Indicates if Kim refinement was applied.
    /// </summary>
    public bool IsRefined { get; init; }

    /// <summary>
    /// Method used for calculation.
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Indicates if boundaries are valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Number of iterations or collocation points used.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// QD+ upper boundary (before refinement).
    /// </summary>
    public double QdUpperBoundary { get; init; }

    /// <summary>
    /// QD+ lower boundary (before refinement).
    /// </summary>
    public double QdLowerBoundary { get; init; }
    
    /// <summary>
    /// Improvement from QD+ to refined (upper boundary).
    /// </summary>
    public double UpperImprovement => IsRefined ? Math.Abs(UpperBoundary - QdUpperBoundary) : 0.0;
    
    /// <summary>
    /// Improvement from QD+ to refined (lower boundary).
    /// </summary>
    public double LowerImprovement => IsRefined ? Math.Abs(LowerBoundary - QdLowerBoundary) : 0.0;
    
    /// <summary>
    /// Optional: Full upper boundary path across time points.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<double>? UpperBoundaryPath { get; init; }

    /// <summary>
    /// Optional: Full lower boundary path across time points.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<double>? LowerBoundaryPath { get; init; }
}