using System;

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
public sealed class DoubleBoundarySolver
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
    public DoubleBoundarySolver(
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
        // Rule 9: Guard Clauses
        if (spot <= 0)
        {
            throw new ArgumentException("Spot price must be positive", nameof(spot));
        }

        if (strike <= 0)
        {
            throw new ArgumentException("Strike price must be positive", nameof(strike));
        }

        if (maturity <= 0)
        {
            throw new ArgumentException("Maturity must be positive", nameof(maturity));
        }

        if (volatility <= 0)
        {
            throw new ArgumentException("Volatility must be positive", nameof(volatility));
        }

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
    /// <returns>Solution containing boundaries and metadata</returns>
    public DoubleBoundaryResult Solve()
    {
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
    /// Calculates initial boundary estimates using QD+ approximation.
    /// </summary>
    private (double upperInitial, double lowerInitial) CalculateInitialBoundaries()
    {
        QdPlusApproximation qdplus = new QdPlusApproximation(
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
        DoubleBoundaryKimSolver kimSolver = new DoubleBoundaryKimSolver(
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
        if (!_isCall)
        {
            // Put: double boundary when q < r < 0
            return _dividendYield < _rate && _rate < 0;
        }
        else
        {
            // Call: double boundary when 0 < r < q
            return 0 < _rate && _rate < _dividendYield;
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
    public double UpperBoundary { get; set; }
    
    /// <summary>
    /// Lower exercise boundary at maturity.
    /// </summary>
    public double LowerBoundary { get; set; }
    
    /// <summary>
    /// Time when boundaries cross (0 if no crossing).
    /// </summary>
    public double CrossingTime { get; set; }
    
    /// <summary>
    /// Indicates if Kim refinement was applied.
    /// </summary>
    public bool IsRefined { get; set; }
    
    /// <summary>
    /// Method used for calculation.
    /// </summary>
    public string Method { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates if boundaries are valid.
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Number of iterations or collocation points used.
    /// </summary>
    public int Iterations { get; set; }
    
    /// <summary>
    /// QD+ upper boundary (before refinement).
    /// </summary>
    public double QdUpperBoundary { get; set; }
    
    /// <summary>
    /// QD+ lower boundary (before refinement).
    /// </summary>
    public double QdLowerBoundary { get; set; }
    
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
    public System.Collections.Generic.IReadOnlyList<double>? UpperBoundaryPath { get; set; }

    /// <summary>
    /// Optional: Full lower boundary path across time points.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<double>? LowerBoundaryPath { get; set; }
}