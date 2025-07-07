using Alaris.Double;

namespace Alaris.Double
{
    /// <summary>
    /// Exercise regime types for American options under general interest rate conditions
    /// </summary>
    public enum ExerciseRegimeType
    {
        /// <summary>Single boundary case: r ≥ q ≥ 0</summary>
        SingleBoundaryPositive,
        
        /// <summary>Single boundary case: r ≥ 0 and q less than 0</summary>
        SingleBoundaryNegativeDividend,
        
        /// <summary>Double boundary case: q less than r less than 0, σ ≤ σ*</summary>
        DoubleBoundaryNegativeRates,
        
        /// <summary>No exercise case: various conditions where early exercise is never optimal</summary>
        NoEarlyExercise,
        
        /// <summary>Degenerate cases requiring special handling</summary>
        Degenerate
    }

    /// <summary>
    /// Comprehensive analysis of American option exercise regimes under general interest rate conditions
    /// Implements the mathematical framework from "The Alaris Mathematical Framework" paper
    /// </summary>
    public static class RegimeAnalyzer
    {
        private const double EPSILON = 1e-12;

        /// <summary>
        /// Determines the exercise regime for American options
        /// </summary>
        /// <param name="r">Risk-free interest rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="sigma">Volatility</param>
        /// <param name="optionType">Option type (Put or Call)</param>
        /// <returns>The appropriate exercise regime</returns>
        public static ExerciseRegimeType DetermineRegime(double r, double q, double sigma, Option.Type optionType)
        {
            if (optionType == Option.Type.Put)
            {
                return DeterminePutRegime(r, q, sigma);
            }
            else
            {
                return DetermineCallRegime(r, q, sigma);
            }
        }

        /// <summary>
        /// Calculates the critical volatility threshold σ* = |√(-2r) - √(-2q)|
        /// Above this threshold, boundaries intersect before maturity
        /// </summary>
        /// <param name="r">Risk-free interest rate (must be negative)</param>
        /// <param name="q">Dividend yield (must be negative)</param>
        /// <returns>Critical volatility, or NaN if parameters invalid</returns>
        public static double CriticalVolatility(double r, double q)
        {
            if (r >= -EPSILON || q >= -EPSILON || r <= q + EPSILON)
            {
                return double.NaN;
            }
            
            return Math.Abs(Math.Sqrt(-2.0 * r) - Math.Sqrt(-2.0 * q));
        }

        /// <summary>
        /// Estimates the boundary intersection time τ* for double boundary cases
        /// Uses numerical root finding when boundaries intersect before maturity
        /// </summary>
        /// <param name="r">Risk-free interest rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="sigma">Volatility</param>
        /// <param name="strike">Strike price</param>
        /// <returns>Time to boundary intersection, or +∞ if no intersection</returns>
        public static double EstimateBoundaryIntersectionTime(double r, double q, double sigma, double strike)
        {
            double sigmaCritical = CriticalVolatility(r, q);
            
            if (double.IsNaN(sigmaCritical) || sigma <= sigmaCritical + EPSILON)
            {
                return double.PositiveInfinity; // Boundaries never intersect
            }

            // Use simplified numerical root finding to avoid SWIG binding issues
            try
            {
                return FindIntersectionTimeNumerical(r, q, sigma, strike);
            }
            catch
            {
                // If numerical solution fails, return analytical approximation
                return EstimateIntersectionTimeAnalytical(r, q, sigma);
            }
        }

        /// <summary>
        /// Computes the characteristic equation roots for American option boundaries
        /// λ = (-μ ± √(μ² + 2rσ²)) / σ²
        /// where μ = r - q - σ²/2
        /// </summary>
        /// <param name="r">Risk-free interest rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="sigma">Volatility</param>
        /// <returns>Tuple of (λ₋, λ₊) roots</returns>
        public static (double lambdaMinus, double lambdaPlus) ComputeCharacteristicRoots(double r, double q, double sigma)
        {
            double mu = r - q - 0.5 * sigma * sigma;
            double discriminant = mu * mu + 2.0 * r * sigma * sigma;
            
            if (discriminant < 0)
            {
                throw new ArgumentException("Invalid parameters: discriminant is negative");
            }
            
            double sqrtDiscriminant = Math.Sqrt(discriminant);
            double lambdaMinus = (-mu - sqrtDiscriminant) / (sigma * sigma);
            double lambdaPlus = (-mu + sqrtDiscriminant) / (sigma * sigma);
            
            return (lambdaMinus, lambdaPlus);
        }

        /// <summary>
        /// Calculates the perpetual American option boundary
        /// B∞ = K * λ / (λ - 1) for puts, where λ is the appropriate characteristic root
        /// </summary>
        /// <param name="strike">Strike price</param>
        /// <param name="r">Risk-free interest rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="sigma">Volatility</param>
        /// <param name="optionType">Option type</param>
        /// <returns>Perpetual boundary level</returns>
        public static double PerpetualBoundary(double strike, double r, double q, double sigma, Option.Type optionType)
        {
            var (lambdaMinus, lambdaPlus) = ComputeCharacteristicRoots(r, q, sigma);
            
            if (optionType == Option.Type.Put)
            {
                if (Math.Abs(lambdaMinus - 1.0) < EPSILON)
                {
                    throw new ArgumentException("Degenerate case: λ₋ = 1");
                }
                return strike * lambdaMinus / (lambdaMinus - 1.0);
            }
            else
            {
                if (Math.Abs(lambdaPlus - 1.0) < EPSILON)
                {
                    throw new ArgumentException("Degenerate case: λ₊ = 1");
                }
                return strike * lambdaPlus / (lambdaPlus - 1.0);
            }
        }

        /// <summary>
        /// Validates that parameters are suitable for the given regime
        /// </summary>
        /// <param name="regime">Exercise regime</param>
        /// <param name="r">Risk-free interest rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="sigma">Volatility</param>
        /// <returns>True if parameters are valid for the regime</returns>
        public static bool ValidateRegimeParameters(ExerciseRegimeType regime, double r, double q, double sigma)
        {
            return regime switch
            {
                ExerciseRegimeType.SingleBoundaryPositive => r >= q - EPSILON && r >= -EPSILON,
                ExerciseRegimeType.SingleBoundaryNegativeDividend => r >= -EPSILON && q < -EPSILON,
                ExerciseRegimeType.DoubleBoundaryNegativeRates => q < r - EPSILON && r < -EPSILON && 
                                                                sigma <= CriticalVolatility(r, q) + EPSILON,
                ExerciseRegimeType.NoEarlyExercise => true, // Can occur in various parameter ranges
                ExerciseRegimeType.Degenerate => true, // Special cases
                _ => false
            };
        }

        private static ExerciseRegimeType DeterminePutRegime(double r, double q, double sigma)
        {
            if (r >= q - EPSILON && r >= -EPSILON)
            {
                return ExerciseRegimeType.SingleBoundaryPositive;
            }
            
            if (r >= -EPSILON && q < -EPSILON)
            {
                return ExerciseRegimeType.SingleBoundaryNegativeDividend;
            }
            
            if (q < r - EPSILON && r < -EPSILON)
            {
                double sigmaCritical = CriticalVolatility(r, q);
                
                if (double.IsNaN(sigmaCritical))
                {
                    return ExerciseRegimeType.Degenerate;
                }
                
                return sigma <= sigmaCritical + EPSILON ? 
                    ExerciseRegimeType.DoubleBoundaryNegativeRates : 
                    ExerciseRegimeType.NoEarlyExercise;
            }
            
            if (r <= q + EPSILON && r < -EPSILON)
            {
                return ExerciseRegimeType.NoEarlyExercise;
            }
            
            return ExerciseRegimeType.Degenerate;
        }

        private static ExerciseRegimeType DetermineCallRegime(double r, double q, double sigma)
        {
            // For calls, early exercise is optimal when q > r (dividend exceeds interest)
            if (q > r + EPSILON)
            {
                return r >= -EPSILON ? 
                    ExerciseRegimeType.SingleBoundaryPositive : 
                    ExerciseRegimeType.SingleBoundaryNegativeDividend;
            }
            else
            {
                return ExerciseRegimeType.NoEarlyExercise;
            }
        }

        private static double EstimateIntersectionTimeAnalytical(double r, double q, double sigma)
        {
            // Analytical approximation based on asymptotic analysis
            double mu = r - q - 0.5 * sigma * sigma;
            double discriminant = mu * mu + 2.0 * r * sigma * sigma;
            
            if (discriminant <= 0)
            {
                return double.PositiveInfinity;
            }
            
            // Rough estimate based on boundary evolution rates
            return Math.Max(0.1, -2.0 * Math.Log(Math.Abs(r / q)) / (sigma * sigma));
        }

        /// <summary>
        /// Simple numerical root finding for boundary intersection time
        /// Uses bisection method to avoid SWIG binding issues with QuantLib solvers
        /// </summary>
        private static double FindIntersectionTimeNumerical(double r, double q, double sigma, double strike)
        {
            const double tolerance = 1e-8;
            const int maxIterations = 100;
            
            double lowerBound = 0.001; // 1 day
            double upperBound = 10.0;  // 10 years
            
            // Check if root exists in the interval
            double fLower = EvaluateBoundaryDifference(lowerBound, r, q, sigma, strike);
            double fUpper = EvaluateBoundaryDifference(upperBound, r, q, sigma, strike);
            
            if (fLower * fUpper > 0)
            {
                // No root in interval, return analytical estimate
                return EstimateIntersectionTimeAnalytical(r, q, sigma);
            }
            
            // Bisection method
            for (int iter = 0; iter < maxIterations; iter++)
            {
                double midPoint = 0.5 * (lowerBound + upperBound);
                double fMid = EvaluateBoundaryDifference(midPoint, r, q, sigma, strike);
                
                if (Math.Abs(fMid) < tolerance || Math.Abs(upperBound - lowerBound) < tolerance)
                {
                    return midPoint;
                }
                
                if (fLower * fMid < 0)
                {
                    upperBound = midPoint;
                    fUpper = fMid;
                }
                else
                {
                    lowerBound = midPoint;
                    fLower = fMid;
                }
            }
            
            return 0.5 * (lowerBound + upperBound);
        }

        /// <summary>
        /// Evaluates the difference B(τ) - Y(τ) for boundary intersection finding
        /// </summary>
        private static double EvaluateBoundaryDifference(double tau, double r, double q, double sigma, double strike)
        {
            // This is a simplified approximation using asymptotic boundary behavior
            // In practice, this would use the full spectral boundary representations
            var (lambdaMinus, lambdaPlus) = ComputeCharacteristicRoots(r, q, sigma);
            
            double upperAsymptotic = strike * lambdaMinus / (lambdaMinus - 1.0);
            double lowerAsymptotic = strike * lambdaPlus / (lambdaPlus - 1.0);
            
            // Simple exponential approach to asymptotic values
            double upperBoundary = strike + (upperAsymptotic - strike) * (1.0 - Math.Exp(-tau));
            double lowerBoundary = strike * r / q + (lowerAsymptotic - strike * r / q) * (1.0 - Math.Exp(-tau));
            
            return upperBoundary - lowerBoundary;
        }
    }
}