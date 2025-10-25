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
    /// Solves for both boundaries.
    /// </summary>
    /// <returns>Result containing upper and lower boundaries at t=0, crossing time, and metadata</returns>
    public DoubleBoundaryResult Solve()
    {
        // Stage 1: QD+ approximation with Super Halley's method
        var qdSolver = new QdPlusApproximation(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
        
        var (upperQd, lowerQd) = qdSolver.CalculateBoundaries();
        
        // Check if QD+ boundaries are valid
        bool qdValid = IsValidBoundaryPair(upperQd, lowerQd);
        
        if (!_useRefinement || !qdValid)
        {
            // Return QD+ approximation only
            return new DoubleBoundaryResult
            {
                UpperBoundary = upperQd,
                LowerBoundary = lowerQd,
                CrossingTime = 0.0,
                Method = "QD+ only",
                IsRefined = false,
                IsValid = qdValid
            };
        }
        
        // Stage 2: FP-B' Kim solver refinement
        var kimSolver = new DoubleBoundaryKimSolver(
            _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall, _collocationPoints);
        
        var (upperArray, lowerArray, crossingTime) = kimSolver.SolveBoundaries(upperQd, lowerQd);
        
        // Boundary at t=0 is the first element
        double upperRefined = upperArray[0];
        double lowerRefined = lowerArray[0];
        
        bool refinedValid = IsValidBoundaryPair(upperRefined, lowerRefined);
        
        return new DoubleBoundaryResult
        {
            UpperBoundary = upperRefined,
            LowerBoundary = lowerRefined,
            CrossingTime = crossingTime,
            Method = "QD+ with FP-B' refinement",
            IsRefined = true,
            IsValid = refinedValid,
            QdUpperBoundary = upperQd,
            QdLowerBoundary = lowerQd
        };
    }
    
    /// <summary>
    /// Validates that boundaries don't cross.
    /// </summary>
    private bool IsValidBoundaryPair(double upper, double lower)
    {
        if (_isCall)
        {
            // For calls: upper > lower, both >= strike
            return upper > lower && upper >= _strike && lower >= _strike;
        }
        else
        {
            // For puts: upper > lower, both <= strike
            return upper > lower && upper <= _strike && lower <= _strike;
        }
    }
}

/// <summary>
/// Result from double boundary solver containing boundaries and diagnostic information.
/// </summary>
public sealed class DoubleBoundaryResult
{
    /// <summary>
    /// Upper exercise boundary at t=0.
    /// </summary>
    public double UpperBoundary { get; init; }
    
    /// <summary>
    /// Lower exercise boundary at t=0.
    /// </summary>
    public double LowerBoundary { get; init; }
    
    /// <summary>
    /// Estimated crossing time (0 if boundaries don't cross).
    /// </summary>
    public double CrossingTime { get; init; }
    
    /// <summary>
    /// Method used: "QD+ only" or "QD+ with FP-B' refinement".
    /// </summary>
    public string Method { get; init; } = string.Empty;
    
    /// <summary>
    /// Whether Kim FP-B' refinement was applied.
    /// </summary>
    public bool IsRefined { get; init; }
    
    /// <summary>
    /// Whether the boundaries are valid (don't cross at t=0).
    /// </summary>
    public bool IsValid { get; init; }
    
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
    public double UpperImprovement => System.Math.Abs(UpperBoundary - QdUpperBoundary);
    
    /// <summary>
    /// Improvement from QD+ to refined (lower boundary).
    /// </summary>
    public double LowerImprovement => System.Math.Abs(LowerBoundary - QdLowerBoundary);
}