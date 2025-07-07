using Alaris.Double;

namespace Alaris.Double;

/// <summary>
/// Mathematical and numerical constants for the Alaris Double Boundary Engine
/// All values are calibrated for optimal performance and accuracy
/// </summary>
public static class Constants
{
    #region Mathematical Constants
    
    /// <summary>
    /// Machine epsilon for floating-point comparisons
    /// Used for regime boundary detection and convergence testing
    /// </summary>
    public const double EPSILON = 1e-12;
    
    /// <summary>
    /// Minimum time to maturity for option pricing (1 hour)
    /// Below this threshold, options are treated as expired
    /// </summary>
    public const double MIN_TIME_TO_MATURITY = 1.0 / (365.0 * 24.0);
    
    /// <summary>
    /// Maximum time to maturity for accurate pricing (30 years)
    /// Beyond this, perpetual option approximations are used
    /// </summary>
    public const double MAX_TIME_TO_MATURITY = 30.0;
    
    /// <summary>
    /// Minimum volatility for numerical stability (0.1% per annum)
    /// </summary>
    public const double MIN_VOLATILITY = 0.001;
    
    /// <summary>
    /// Maximum volatility for reasonable option pricing (500% per annum)
    /// </summary>
    public const double MAX_VOLATILITY = 5.0;
    
    /// <summary>
    /// Minimum interest rate for numerical computations (-10% per annum)
    /// Extreme negative rates beyond this may cause numerical instability
    /// </summary>
    public const double MIN_INTEREST_RATE = -0.10;
    
    /// <summary>
    /// Maximum interest rate for reasonable pricing (50% per annum)
    /// </summary>
    public const double MAX_INTEREST_RATE = 0.50;
    
    #endregion

    #region Spectral Method Parameters
    
    /// <summary>
    /// Default number of Chebyshev collocation nodes
    /// Optimal balance between accuracy and computational efficiency
    /// </summary>
    public const int DEFAULT_SPECTRAL_NODES = 8;
    
    /// <summary>
    /// Minimum number of spectral nodes for meaningful approximation
    /// </summary>
    public const int MIN_SPECTRAL_NODES = 3;
    
    /// <summary>
    /// Maximum number of spectral nodes for practical computation
    /// Beyond this, diminishing returns due to roundoff errors
    /// </summary>
    public const int MAX_SPECTRAL_NODES = 32;
    
    /// <summary>
    /// Convergence rate threshold for spectral methods
    /// Below this rate, transformation may be ineffective
    /// </summary>
    public const double MIN_SPECTRAL_CONVERGENCE_RATE = 1.5;
    
    #endregion

    #region Iteration Control Parameters
    
    /// <summary>
    /// Default convergence tolerance for boundary iterations
    /// Provides machine precision accuracy for most applications
    /// </summary>
    public const double DEFAULT_TOLERANCE = 1e-12;
    
    /// <summary>
    /// Relaxed tolerance for fast approximations
    /// </summary>
    public const double FAST_TOLERANCE = 1e-8;
    
    /// <summary>
    /// High precision tolerance for research applications
    /// </summary>
    public const double HIGH_PRECISION_TOLERANCE = 1e-14;
    
    /// <summary>
    /// Default maximum iterations for boundary fixed-point schemes
    /// </summary>
    public const int DEFAULT_MAX_ITERATIONS = 100;
    
    /// <summary>
    /// Fast convergence iteration limit
    /// </summary>
    public const int FAST_MAX_ITERATIONS = 50;
    
    /// <summary>
    /// High precision iteration limit
    /// </summary>
    public const int HIGH_PRECISION_MAX_ITERATIONS = 200;
    
    /// <summary>
    /// Anderson acceleration memory depth
    /// Number of previous iterates to use for acceleration
    /// </summary>
    public const int ANDERSON_MEMORY_DEPTH = 5;
    
    #endregion

    #region Numerical Integration Parameters
    
    /// <summary>
    /// Default tolerance for adaptive quadrature
    /// </summary>
    public const double INTEGRATION_TOLERANCE = 1e-10;
    
    /// <summary>
    /// Maximum number of adaptive quadrature subdivisions
    /// </summary>
    public const int MAX_INTEGRATION_SUBDIVISIONS = 1000;
    
    /// <summary>
    /// Default number of Gauss-Legendre quadrature points
    /// </summary>
    public const int DEFAULT_QUADRATURE_POINTS = 64;
    
    /// <summary>
    /// Step size for numerical derivatives (central difference)
    /// </summary>
    public const double NUMERICAL_DERIVATIVE_STEP = 1e-8;
    
    #endregion

    #region Boundary Initialization Parameters
    
    /// <summary>
    /// Relaxation factor for boundary value updates
    /// Prevents oscillations in fixed-point iterations
    /// </summary>
    public const double BOUNDARY_RELAXATION_FACTOR = 0.7;
    
    /// <summary>
    /// Safety margin for boundary bounds
    /// Ensures boundaries stay within reasonable ranges
    /// </summary>
    public const double BOUNDARY_SAFETY_MARGIN = 0.01;
    
    /// <summary>
    /// Exponential decay rate for boundary initialization
    /// Controls approach to perpetual boundary values
    /// </summary>
    public const double BOUNDARY_DECAY_RATE = 0.5;
    
    #endregion

    #region Root Finding Parameters
    
    /// <summary>
    /// Default tolerance for Brent's method root finding
    /// </summary>
    public const double ROOT_FINDING_TOLERANCE = 1e-12;
    
    /// <summary>
    /// Maximum evaluations for root finding algorithms
    /// </summary>
    public const int MAX_ROOT_FINDING_EVALUATIONS = 1000;
    
    /// <summary>
    /// Initial bracket expansion factor for root finding
    /// </summary>
    public const double ROOT_FINDING_BRACKET_EXPANSION = 1.6;
    
    #endregion

    #region Validation and Benchmarking
    
    /// <summary>
    /// Relative error tolerance for validation against known results
    /// </summary>
    public const double VALIDATION_TOLERANCE = 1e-6;
    
    /// <summary>
    /// Number of test cases for convergence analysis
    /// </summary>
    public const int CONVERGENCE_TEST_CASES = 10;
    
    /// <summary>
    /// Benchmark timeout in milliseconds
    /// Maximum time allowed for performance tests
    /// </summary>
    public const int BENCHMARK_TIMEOUT_MS = 10000;
    
    #endregion

    #region Performance Tuning
    
    /// <summary>
    /// Cache size for boundary function evaluations
    /// Number of recently computed values to cache
    /// </summary>
    public const int BOUNDARY_CACHE_SIZE = 256;
    
    /// <summary>
    /// Parallel processing threshold
    /// Minimum problem size to enable parallel computation
    /// </summary>
    public const int PARALLEL_THRESHOLD = 16;
    
    /// <summary>
    /// Memory allocation chunk size for large arrays
    /// </summary>
    public const int MEMORY_CHUNK_SIZE = 1024;
    
    #endregion
}

/// <summary>
/// Predefined parameter sets for common use cases
/// </summary>
public static class ParameterSets
{
    /// <summary>
    /// Fast approximation parameters for real-time applications
    /// </summary>
    public static class Fast
    {
        public const int SpectralNodes = 6;
        public const double Tolerance = Constants.FAST_TOLERANCE;
        public const int MaxIterations = Constants.FAST_MAX_ITERATIONS;
        public const bool UseAcceleration = false;
    }
    
    /// <summary>
    /// Standard precision parameters for most applications
    /// </summary>
    public static class Standard
    {
        public const int SpectralNodes = Constants.DEFAULT_SPECTRAL_NODES;
        public const double Tolerance = Constants.DEFAULT_TOLERANCE;
        public const int MaxIterations = Constants.DEFAULT_MAX_ITERATIONS;
        public const bool UseAcceleration = true;
    }
    
    /// <summary>
    /// High precision parameters for research and validation
    /// </summary>
    public static class HighPrecision
    {
        public const int SpectralNodes = 12;
        public const double Tolerance = Constants.HIGH_PRECISION_TOLERANCE;
        public const int MaxIterations = Constants.HIGH_PRECISION_MAX_ITERATIONS;
        public const bool UseAcceleration = true;
    }
    
    /// <summary>
    /// Research-grade parameters for maximum accuracy
    /// </summary>
    public static class Research
    {
        public const int SpectralNodes = 16;
        public const double Tolerance = 1e-15;
        public const int MaxIterations = 500;
        public const bool UseAcceleration = true;
    }
}

/// <summary>
/// Known benchmark values for validation
/// Source: Various academic papers and commercial systems
/// </summary>
public static class BenchmarkValues
{
    /// <summary>
    /// Classic American put option benchmark (Haug, 2007)
    /// S=36, K=40, r=0.06, q=0.02, σ=0.20, T=1.0
    /// </summary>
    public static class ClassicAmericanPut
    {
        public const double Spot = 36.0;
        public const double Strike = 40.0;
        public const double Rate = 0.06;
        public const double Dividend = 0.02;
        public const double Volatility = 0.20;
        public const double TimeToMaturity = 1.0;
        public const double ExpectedPrice = 4.48927603; // High precision reference
        public const double ExpectedBoundary = 35.6733; // At 6 months
    }
    
    /// <summary>
    /// Double boundary test case (Healy, 2021)
    /// S=100, K=100, r=-0.01, q=-0.02, σ=0.15, T=0.5
    /// </summary>
    public static class DoubleBoundaryCase
    {
        public const double Spot = 100.0;
        public const double Strike = 100.0;
        public const double Rate = -0.01;
        public const double Dividend = -0.02;
        public const double Volatility = 0.15;
        public const double TimeToMaturity = 0.5;
        public const double CriticalVolatility = 0.059; // Approximate σ*
        public const double ExpectedUpperBoundary = 105.2; // At 3 months
        public const double ExpectedLowerBoundary = 94.8;  // At 3 months
    }
    
    /// <summary>
    /// Critical volatility validation cases
    /// </summary>
    public static class CriticalVolatilityCases
    {
        public static readonly (double r, double q, double sigmaCritical)[] TestCases = 
        {
            (-0.005, -0.01, 0.0316), // √(0.01) - √(0.02) ≈ 0.0316
            (-0.01, -0.02, 0.0590),  // √(0.02) - √(0.04) ≈ 0.0590
            (-0.02, -0.03, 0.0316),  // √(0.04) - √(0.06) ≈ 0.0316
        };
    }
}

/// <summary>
/// Error messages and diagnostic information
/// </summary>
public static class ErrorMessages
{
    public const string INVALID_PARAMETERS = "Invalid market parameters for American option pricing";
    public const string CONVERGENCE_FAILED = "Fixed-point iteration failed to converge within maximum iterations";
    public const string UNSUPPORTED_REGIME = "Exercise regime not supported by this engine";
    public const string NUMERICAL_INSTABILITY = "Numerical instability detected in boundary computation";
    public const string INTEGRATION_FAILED = "Numerical integration failed to achieve required tolerance";
    public const string TRANSFORMATION_FAILED = "Spectral transformation produced invalid results";
    public const string MEMORY_ALLOCATION_FAILED = "Failed to allocate memory for large computation";
    public const string TIMEOUT_EXCEEDED = "Computation timeout exceeded maximum allowed time";
}

/// <summary>
/// Logging categories for diagnostic output
/// </summary>
public static class LogCategories
{
    public const string REGIME_DETECTION = "RegimeDetection";
    public const string BOUNDARY_COMPUTATION = "BoundaryComputation";
    public const string SPECTRAL_METHODS = "SpectralMethods";
    public const string INTEGRATION = "Integration";
    public const string CONVERGENCE = "Convergence";
    public const string PERFORMANCE = "Performance";
    public const string VALIDATION = "Validation";
    public const string ERROR_HANDLING = "ErrorHandling";
}