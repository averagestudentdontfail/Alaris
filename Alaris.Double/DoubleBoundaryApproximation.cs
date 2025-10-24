using System;

namespace Alaris.Double
{
    /// <summary>
    /// Provides analytical approximations for American option early exercise boundaries 
    /// under negative interest rate regimes using the QD+ algorithm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements the double boundary approximation methodology from:
    /// Healy, J. (2021). "Pricing American Options Under Negative Rates"
    /// Specifically adapts the Li (2005) QD+ algorithm for negative rate environments.
    /// </para>
    /// <para>
    /// The approximation is valid when q &lt; r &lt; 0, where two exercise boundaries exist.
    /// For other regimes, use single boundary methods or European pricing.
    /// </para>
    /// </remarks>
    public sealed class DoubleBoundaryApproximation
    {
        private readonly double _spot;
        private readonly double _strike;
        private readonly double _maturity;
        private readonly double _rate;
        private readonly double _dividendYield;
        private readonly double _volatility;
        private readonly bool _isCall;
        
        /// <summary>
        /// Initializes a new instance of the DoubleBoundaryApproximation class.
        /// </summary>
        /// <param name="spot">Current asset price S₀</param>
        /// <param name="strike">Strike price K</param>
        /// <param name="maturity">Time to maturity T (in years)</param>
        /// <param name="rate">Risk-free interest rate r</param>
        /// <param name="dividendYield">Continuous dividend yield q</param>
        /// <param name="volatility">Volatility σ</param>
        /// <param name="isCall">True for call options, false for put options</param>
        /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
        public DoubleBoundaryApproximation(
            double spot,
            double strike,
            double maturity,
            double rate,
            double dividendYield,
            double volatility,
            bool isCall)
        {
            if (spot <= 0)
                throw new ArgumentException("Spot price must be positive", nameof(spot));
            if (strike <= 0)
                throw new ArgumentException("Strike price must be positive", nameof(strike));
            if (maturity <= 0)
                throw new ArgumentException("Maturity must be positive", nameof(maturity));
            if (volatility <= 0)
                throw new ArgumentException("Volatility must be positive", nameof(volatility));
            
            _spot = spot;
            _strike = strike;
            _maturity = maturity;
            _rate = rate;
            _dividendYield = dividendYield;
            _volatility = volatility;
            _isCall = isCall;
        }
        
        /// <summary>
        /// Calculates both exercise boundaries using the QD+ approximation method.
        /// </summary>
        /// <returns>
        /// A result containing the upper and lower boundaries, or indication if boundaries cross.
        /// When boundaries cross, the approximation is invalid and European pricing should be used.
        /// </returns>
        /// <remarks>
        /// Implements Healy (2021) Equations 9-14 using Super Halley's method for convergence.
        /// The two boundaries are solved independently as separate systems.
        /// </remarks>
        public BoundaryResult CalculateBoundaries()
        {
            var solver = new QdPlusApproximation(
                _spot, _strike, _maturity, _rate, _dividendYield, _volatility, _isCall);
            
            var (upper, lower) = solver.CalculateBoundaries();
            
            // Check if boundaries are valid (didn't cross)
            bool boundariesCross = _isCall ? (upper <= lower) : (lower >= upper);
            
            return new BoundaryResult
            {
                UpperBoundary = upper,
                LowerBoundary = lower,
                BoundariesCross = boundariesCross,
                IsValid = !boundariesCross
            };
        }
        
        /// <summary>
        /// Approximates the American option value using the calculated boundaries.
        /// </summary>
        /// <returns>
        /// The approximate American option value, or European value if approximation fails.
        /// </returns>
        /// <remarks>
        /// Uses Healy (2021) Equation 13 for the early exercise premium:
        /// e(S) = a₁·S^λ₁·1_{S≥S*₁} + a₂·S^λ₂·1_{S≤S*₂}
        /// </remarks>
        public double ApproximateValue()
        {
            var boundaries = CalculateBoundaries();
            
            // If boundaries cross or are invalid, return European value
            if (boundaries.BoundariesCross || !boundaries.IsValid)
            {
                return CalculateEuropeanValue();
            }
            
            // Check immediate exercise conditions
            if (ShouldExerciseImmediately(boundaries))
            {
                return CalculateIntrinsicValue();
            }
            
            // Calculate early exercise premium and add to European value
            double europeanValue = CalculateEuropeanValue();
            double earlyExercisePremium = CalculateEarlyExercisePremium(boundaries);
            
            return europeanValue + earlyExercisePremium;
        }
        
        /// <summary>
        /// Determines if the option should be exercised immediately based on boundaries.
        /// </summary>
        private bool ShouldExerciseImmediately(BoundaryResult boundaries)
        {
            if (_isCall)
            {
                // For calls: exercise if S ≥ upper boundary
                return _spot >= boundaries.UpperBoundary;
            }
            else
            {
                // For puts: exercise if S ≤ lower boundary
                return _spot <= boundaries.LowerBoundary;
            }
        }
        
        /// <summary>
        /// Calculates the early exercise premium using the QD+ approximation.
        /// </summary>
        /// <remarks>
        /// Implements Healy (2021) Equation 13 with coefficients determined by
        /// the continuity conditions at the boundaries (Equations 11-12).
        /// </remarks>
        private double CalculateEarlyExercisePremium(BoundaryResult boundaries)
        {
            // Calculate lambdas
            var (lambda1, lambda2) = CalculateLambdas();
            
            // Determine which boundary is relevant for premium calculation
            bool useUpperBoundary = _isCall ? (_spot >= boundaries.UpperBoundary) 
                                             : (_spot <= boundaries.LowerBoundary);
            
            if (useUpperBoundary)
            {
                // Calculate coefficient a₁ for upper boundary
                double a1 = CalculateUpperBoundaryCoefficient(boundaries.UpperBoundary, lambda1);
                return a1 * Math.Pow(_spot, lambda1);
            }
            else
            {
                // Calculate coefficient a₂ for lower boundary
                double a2 = CalculateLowerBoundaryCoefficient(boundaries.LowerBoundary, lambda2);
                return a2 * Math.Pow(_spot, lambda2);
            }
        }
        
        /// <summary>
        /// Calculates the lambda values from Healy (2021) Equation 9.
        /// </summary>
        private (double lambda1, double lambda2) CalculateLambdas()
        {
            double h = 1.0 - Math.Exp(-_rate * _maturity);
            double sigma2 = _volatility * _volatility;
            double alpha = 2.0 * _rate / sigma2;
            double beta = 2.0 * (_rate - _dividendYield) / sigma2;
            
            double discriminant = Math.Sqrt((beta - 1) * (beta - 1) + 4.0 * alpha / h);
            double lambda1 = (-(beta - 1) - discriminant) / 2.0;  // Negative root
            double lambda2 = (-(beta - 1) + discriminant) / 2.0;  // Positive root
            
            return (lambda1, lambda2);
        }
        
        /// <summary>
        /// Calculates the coefficient a₁ using boundary continuity conditions.
        /// </summary>
        private double CalculateUpperBoundaryCoefficient(double boundary, double lambda)
        {
            double eta = _isCall ? 1.0 : -1.0;
            double intrinsic = eta * (boundary - _strike);
            double europeanValue = CalculateEuropeanValue(boundary);
            
            // From continuity: η(S* - K) = VE(S*) + a₁(S*)^λ₁
            // Therefore: a₁ = [η(S* - K) - VE(S*)] / (S*)^λ₁
            double numerator = intrinsic - europeanValue;
            double denominator = Math.Pow(boundary, lambda);
            
            return denominator != 0 ? numerator / denominator : 0.0;
        }
        
        /// <summary>
        /// Calculates the coefficient a₂ using boundary continuity conditions.
        /// </summary>
        private double CalculateLowerBoundaryCoefficient(double boundary, double lambda)
        {
            double eta = _isCall ? 1.0 : -1.0;
            double intrinsic = eta * (boundary - _strike);
            double europeanValue = CalculateEuropeanValue(boundary);
            
            // From continuity: η(S* - K) = VE(S*) + a₂(S*)^λ₂
            // Therefore: a₂ = [η(S* - K) - VE(S*)] / (S*)^λ₂
            double numerator = intrinsic - europeanValue;
            double denominator = Math.Pow(boundary, lambda);
            
            return denominator != 0 ? numerator / denominator : 0.0;
        }
        
        /// <summary>
        /// Calculates European option value using Black-Scholes formula.
        /// </summary>
        private double CalculateEuropeanValue(double? spot = null)
        {
            double S = spot ?? _spot;
            double d1 = CalculateD1(S);
            double d2 = d1 - _volatility * Math.Sqrt(_maturity);
            
            double discountFactor = Math.Exp(-_rate * _maturity);
            double dividendFactor = Math.Exp(-_dividendYield * _maturity);
            
            if (_isCall)
            {
                return S * dividendFactor * NormalCDF(d1) 
                     - _strike * discountFactor * NormalCDF(d2);
            }
            else
            {
                return _strike * discountFactor * NormalCDF(-d2) 
                     - S * dividendFactor * NormalCDF(-d1);
            }
        }
        
        /// <summary>
        /// Calculates intrinsic value (immediate exercise value).
        /// </summary>
        private double CalculateIntrinsicValue()
        {
            return _isCall 
                ? Math.Max(_spot - _strike, 0.0) 
                : Math.Max(_strike - _spot, 0.0);
        }
        
        /// <summary>
        /// Calculates d₁ from Black-Scholes formula.
        /// </summary>
        private double CalculateD1(double S)
        {
            double numerator = Math.Log(S / _strike) 
                             + (_rate - _dividendYield + 0.5 * _volatility * _volatility) * _maturity;
            return numerator / (_volatility * Math.Sqrt(_maturity));
        }
        
        /// <summary>
        /// Standard normal cumulative distribution function.
        /// </summary>
        private double NormalCDF(double x)
        {
            if (x > 8.0) return 1.0;
            if (x < -8.0) return 0.0;
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }
        
        /// <summary>
        /// Error function approximation (Abramowitz and Stegun).
        /// </summary>
        private double Erf(double x)
        {
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;
            
            int sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);
            
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
            
            return sign * y;
        }
    }
    
    /// <summary>
    /// Represents the result of a boundary calculation for double boundary options.
    /// </summary>
    public sealed class BoundaryResult
    {
        /// <summary>
        /// The upper exercise boundary S*₁.
        /// </summary>
        public double UpperBoundary { get; init; }
        
        /// <summary>
        /// The lower exercise boundary S*₂.
        /// </summary>
        public double LowerBoundary { get; init; }
        
        /// <summary>
        /// Indicates whether the boundaries cross (making the approximation invalid).
        /// </summary>
        public bool BoundariesCross { get; init; }
        
        /// <summary>
        /// Indicates whether the boundary calculation is valid.
        /// </summary>
        /// <remarks>
        /// When false, European pricing should be used instead.
        /// </remarks>
        public bool IsValid { get; init; }
        
        /// <summary>
        /// Returns a string representation of the boundary result.
        /// </summary>
        public override string ToString()
        {
            return $"Upper: {UpperBoundary:F4}, Lower: {LowerBoundary:F4}, " +
                   $"Valid: {IsValid}, Cross: {BoundariesCross}";
        }
    }
}