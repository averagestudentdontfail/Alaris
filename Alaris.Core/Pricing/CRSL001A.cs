// CRSL001A.cs - Two-stage solver: QD+ initial + FP-B' Kim refinement
// Component ID: CRSL001A
// Migrated from: Alaris.Double.DBSL001A
//
// NOTE: Kim refinement (Stage 2) requires CRSL002A which is pending migration.
// For now, useRefinement defaults to FALSE to use QD+-only mode.

using System;
using Alaris.Core.Validation;

namespace Alaris.Core.Pricing;

/// <summary>
/// Complete solver for double boundary options. Stage 1: QD+ (fast). Stage 2: FP-B' Kim (accurate).
/// </summary>
public sealed class CRSL001A
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
    /// <param name="useRefinement">Use FP-B' Kim refinement (default FALSE until CRSL002A migrated)</param>
    /// <exception cref="BoundsViolationException">Thrown if inputs violate algorithm bounds.</exception>
    public CRSL001A(
        double spot,
        double strike,
        double maturity,
        double rate,
        double dividendYield,
        double volatility,
        bool isCall,
        int collocationPoints = 50,
        bool useRefinement = true) // Kim refinement now available via CRSL002A
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
        CREX001A nearExpiryHandler = new CREX001A();
        NearExpiryValidation validation = nearExpiryHandler.Validate(_maturity);

        if (validation.Recommendation == NearExpiryRecommendation.UseIntrinsic ||
            validation.Recommendation == NearExpiryRecommendation.UseBlended)
        {
            // Very near expiry or in blending zone: return intrinsic-based boundaries
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
    private DoubleBoundaryResult CreateNearExpiryResult(NearExpiryValidation validation)
    {
        double theoreticalSpread = System.Math.Max(0.01, _volatility * System.Math.Sqrt(_maturity));

        double nearExpiryUpper = _isCall
            ? _strike * (1.0 + (0.3 * theoreticalSpread))
            : _strike * (1.0 - (0.3 * theoreticalSpread));

        double nearExpiryLower = _isCall
            ? _strike * (1.0 + (0.1 * theoreticalSpread))
            : _strike * (1.0 - theoreticalSpread);

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
    /// Calculates initial boundary estimates using QD+ approximation (CRAP001A).
    /// </summary>
    private (double upperInitial, double lowerInitial) CalculateInitialBoundaries()
    {
        CRAP001A qdplus = new CRAP001A(
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
    /// Applies Kim refinement to initial QD+ boundaries using CRSL002A.
    /// </summary>
    private DoubleBoundaryResult ApplyKimRefinement(double upperInitial, double lowerInitial)
    {
        CRSL002A kimSolver = new CRSL002A(
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
    private bool DetectDoubleBoundaryRegime()
    {
        const double HysteresisEpsilon = 0.0005;
        
        if (!_isCall)
        {
            bool rateClearlyNegative = _rate < -HysteresisEpsilon;
            bool dividendClearlyBelowRate = _dividendYield < _rate - HysteresisEpsilon;
            return dividendClearlyBelowRate && rateClearlyNegative;
        }
        else
        {
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
    public double UpperImprovement => IsRefined ? System.Math.Abs(UpperBoundary - QdUpperBoundary) : 0.0;
    
    /// <summary>
    /// Improvement from QD+ to refined (lower boundary).
    /// </summary>
    public double LowerImprovement => IsRefined ? System.Math.Abs(LowerBoundary - QdLowerBoundary) : 0.0;
    
    /// <summary>
    /// Optional: Full upper boundary path across time points.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<double>? UpperBoundaryPath { get; init; }

    /// <summary>
    /// Optional: Full lower boundary path across time points.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<double>? LowerBoundaryPath { get; init; }
}
