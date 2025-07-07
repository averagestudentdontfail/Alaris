using Alaris.Quantlib;
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

            // Use QuantLib's Brent solver to find intersection time
            var solver = new Brent();
            solver.setMaxEvaluations(1000);
            
            try
            {
                // Function that equals zero when boundaries intersect: B(τ) - Y(τ) = 0
                var intersectionFunction = new IntersectionFunction(r, q, sigma, strike);
                
                // Search in reasonable time range
                double minTime = 0.001; // 1 day
                double maxTime = 10.0;  // 10 years
                
                return solver.solve(intersectionFunction, 1e-8, minTime, maxTime, 1.0);
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
    }

    /// <summary>
    /// Function class for finding boundary intersection time using QuantLib solvers
    /// </summary>
    internal class IntersectionFunction : UnaryFunction
    {
        private readonly double _r, _q, _sigma, _strike;

        public IntersectionFunction(double r, double q, double sigma, double strike)
        {
            _r = r;
            _q = q;
            _sigma = sigma;
            _strike = strike;
        }

        public override double value(double tau)
        {
            // This would compute B(τ) - Y(τ) using the spectral boundary representations
            // For now, use simplified analytical approximation
            var (lambdaMinus, lambdaPlus) = RegimeAnalyzer.ComputeCharacteristicRoots(_r, _q, _sigma);
            
            double upperAsymptotic = _strike * lambdaMinus / (lambdaMinus - 1.0);
            double lowerAsymptotic = _strike * lambdaPlus / (lambdaPlus - 1.0);
            
            // Simple exponential approach to asymptotic values
            double upperBoundary = _strike + (upperAsymptotic - _strike) * (1.0 - Math.Exp(-tau));
            double lowerBoundary = _strike * _r / _q + (lowerAsymptotic - _strike * _r / _q) * (1.0 - Math.Exp(-tau));
            
            return upperBoundary - lowerBoundary;
        }
    }
}