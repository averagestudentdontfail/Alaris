// Alaris.Double/DoubleBoundaryApproximation.cs
using System;

namespace Alaris.Double
{
    /// <summary>
    /// Computes initial boundary estimates for the double boundary case using
    /// an adapted QD+ algorithm from Li (2010) as modified by Healy (2021).
    /// 
    /// The two boundaries are computed independently using different roots of the
    /// characteristic equation (lambda1 for upper, lambda2 for lower).
    /// 
    /// Reference: Healy Section 4.1
    /// </summary>
    public class DoubleBoundaryApproximation
    {
        private readonly GeneralizedBlackScholesProcess _process;
        private readonly double _K, _T, _r, _q, _sigma;
        
        private const int MAX_ITERATIONS = 50;
        private const double TOLERANCE = 1e-6;
        
        public DoubleBoundaryApproximation(
            GeneralizedBlackScholesProcess process,
            double K, double T, double r, double q, double sigma)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _K = K;
            _T = T;
            _r = r;
            _q = q;
            _sigma = sigma;
        }
        
        /// <summary>
        /// Computes initial approximations for both boundaries.
        /// </summary>
        /// <param name="m">Number of time points</param>
        /// <returns>Boundary result with upper/lower boundaries and crossing time</returns>
        public BoundaryResult ComputeInitialBoundaries(int m = 100)
        {
            double[] upperBoundary = new double[m];
            double[] lowerBoundary = new double[m];
            double dt = _T / (m - 1);
            double crossingTime = 0;
            
            // Backward iteration from maturity to present
            for (int i = m - 1; i >= 0; i--)
            {
                double t = i * dt;
                double tau = _T - t;
                
                if (tau < 1e-10)
                {
                    // At maturity (Healy Section 3)
                    upperBoundary[i] = _K;
                    lowerBoundary[i] = _K * Math.Min(1.0, _r / _q);
                    continue;
                }
                
                double h = 1 - Math.Exp(-_r * tau);
                
                // Calculate characteristic equation roots
                double lambda1 = CalculateLambda1(h); // Negative root for upper
                double lambda2 = CalculateLambda2(h); // Positive root for lower
                
                // Initial guesses
                double upperGuess = i < m - 1 ? upperBoundary[i + 1] : _K;
                double lowerGuess = i < m - 1 ? lowerBoundary[i + 1] : _K * Math.Min(1.0, _r / _q);
                
                // Solve independent QD+ equations
                upperBoundary[i] = SolveQdPlus(tau, h, lambda1, upperGuess, isUpper: true);
                lowerBoundary[i] = SolveQdPlus(tau, h, lambda2, lowerGuess, isUpper: false);
                
                // Detect crossing
                if (crossingTime == 0 && upperBoundary[i] <= lowerBoundary[i])
                {
                    crossingTime = t;
                }
            }
            
            // Adjust for crossings if needed
            if (crossingTime > 0)
            {
                AdjustForCrossing(upperBoundary, lowerBoundary, crossingTime, _T);
            }
            
            return new BoundaryResult
            {
                UpperBoundary = upperBoundary,
                LowerBoundary = lowerBoundary,
                CrossingTime = crossingTime
            };
        }
        
        private double CalculateLambda1(double h)
        {
            // Healy Equation 9 - negative root
            double alpha = Alpha;
            double beta = Beta;
            double discriminant = (beta - 1) * (beta - 1) + 4 * alpha / h;
            return (-(beta - 1) - Math.Sqrt(discriminant)) / 2;
        }
        
        private double CalculateLambda2(double h)
        {
            // Healy Equation 9 - positive root
            double alpha = Alpha;
            double beta = Beta;
            double discriminant = (beta - 1) * (beta - 1) + 4 * alpha / h;
            return (-(beta - 1) + Math.Sqrt(discriminant)) / 2;
        }
        
        private double SolveQdPlus(double tau, double h, double lambda, double initialGuess, bool isUpper)
        {
            // Healy Equation 14 - Li refinement of QD+ boundary
            // Using Super Halley's method for stability (Section 4.2)
            
            double S_star = initialGuess;
            
            for (int iter = 0; iter < MAX_ITERATIONS; iter++)
            {
                // European option values at boundary
                double d1 = D1(S_star, _K, tau);
                double d2 = d1 - _sigma * Math.Sqrt(tau);
                
                double V_E = _K * Math.Exp(-_r * tau) * CDF(-d2) 
                           - S_star * Math.Exp(-_q * tau) * CDF(-d1);
                double dV_dS = -Math.Exp(-_q * tau) * CDF(-d1);
                
                // Calculate c0 term (Healy Equation 14)
                double theta = CalculateTheta(S_star, _K, tau);
                double c0 = CalculateC0(h, lambda, S_star, tau, theta, V_E);
                
                // QD+ equation: high contact condition
                double phi_d1 = PDF(d1);
                double rhs = -Math.Exp(-_q * tau) * CDF(-d1) 
                           + (lambda + c0) * ((_K - S_star) - V_E) / S_star;
                
                double f = dV_dS - rhs - (-1); // Should equal zero at boundary
                
                if (Math.Abs(f) < TOLERANCE)
                    break;
                
                // Super Halley iteration (more stable than Newton)
                double df_dS = NumericalDerivative(S_star, tau, lambda, c0);
                double d2f_dS2 = NumericalSecondDerivative(S_star, tau);
                
                double L_f = f * d2f_dS2 / (df_dS * df_dS);
                double denominator = 1 - L_f;
                
                if (Math.Abs(denominator) < 1e-10)
                    denominator = 1e-10; // Prevent division by zero
                
                double update = (f / df_dS) * (1 + 0.5 * L_f / denominator);
                S_star = S_star - update;
                
                // Keep boundary in reasonable range
                if (isUpper)
                    S_star = Math.Max(S_star, _K * 0.3);
                else
                    S_star = Math.Min(Math.Max(S_star, _K * 0.1), _K);
            }
            
            return S_star;
        }
        
        private double CalculateC0(double h, double lambda, double S_star, double tau, double theta, double V_E)
        {
            double intrinsic = _K - S_star;
            double premium = intrinsic - V_E;
            
            if (Math.Abs(premium) < 1e-10)
                return 0;
            
            double alpha = Alpha;
            double beta = Beta;
            
            double term1 = -(1 - h) * alpha / (2 * lambda + beta - 1);
            double term2 = (1.0 / h - theta / (_r * premium));
            double term3 = LambdaPrime(h, lambda) / (2 * lambda + beta - 1);
            
            return term1 * (term2 + term3);
        }
        
        private double LambdaPrime(double h, double lambda)
        {
            // Derivative of lambda with respect to h
            double alpha = Alpha;
            double beta = Beta;
            double discriminant = (beta - 1) * (beta - 1) + 4 * alpha / h;
            
            if (discriminant < 1e-10)
                return 0;
            
            return -2 * alpha / (h * h * Math.Sqrt(discriminant));
        }
        
        private double CalculateTheta(double S, double K, double tau)
        {
            double d1 = D1(S, K, tau);
            double d2 = d1 - _sigma * Math.Sqrt(tau);
            
            double term1 = -S * Math.Exp(-_q * tau) * PDF(d1) * _sigma / (2 * Math.Sqrt(tau));
            double term2 = _q * S * Math.Exp(-_q * tau) * CDF(-d1);
            double term3 = -_r * K * Math.Exp(-_r * tau) * CDF(-d2);
            
            return term1 - term2 - term3;
        }
        
        private double NumericalDerivative(double S, double tau, double lambda, double c0)
        {
            double h = S * 0.0001;
            double f_plus = EvaluateQdPlusEquation(S + h, tau, lambda, c0);
            double f_minus = EvaluateQdPlusEquation(S - h, tau, lambda, c0);
            return (f_plus - f_minus) / (2 * h);
        }
        
        private double NumericalSecondDerivative(double S, double tau)
        {
            double h = S * 0.0001;
            double d1_0 = D1(S, _K, tau);
            double d1_p = D1(S + h, _K, tau);
            double d1_m = D1(S - h, _K, tau);
            
            double dV_dS_0 = -Math.Exp(-_q * tau) * CDF(-d1_0);
            double dV_dS_p = -Math.Exp(-_q * tau) * CDF(-d1_p);
            double dV_dS_m = -Math.Exp(-_q * tau) * CDF(-d1_m);
            
            return (dV_dS_p - 2 * dV_dS_0 + dV_dS_m) / (h * h);
        }
        
        private double EvaluateQdPlusEquation(double S, double tau, double lambda, double c0)
        {
            double d1 = D1(S, _K, tau);
            double d2 = d1 - _sigma * Math.Sqrt(tau);
            
            double V_E = _K * Math.Exp(-_r * tau) * CDF(-d2) 
                       - S * Math.Exp(-_q * tau) * CDF(-d1);
            double dV_dS = -Math.Exp(-_q * tau) * CDF(-d1);
            double rhs = -Math.Exp(-_q * tau) * CDF(-d1) 
                       + (lambda + c0) * ((_K - S) - V_E) / S;
            
            return dV_dS - rhs - (-1);
        }
        
        private void AdjustForCrossing(double[] upper, double[] lower, double ts, double T)
        {
            // Healy Section 5.3 - set boundaries equal before crossing
            int m = upper.Length;
            double dt = T / (m - 1);
            int s = (int)(ts / dt);
            
            if (s >= m) return;
            
            // Use the more conservative value at crossing
            double c_star = Math.Min(Math.Max(upper[s], lower[Math.Min(s + 1, m - 1)]), 
                                    lower[Math.Min(s + 1, m - 1)]);
            
            for (int i = 0; i <= s; i++)
            {
                upper[i] = c_star;
                lower[i] = c_star;
            }
        }
        
        // Helper functions
        private double D1(double S, double K, double tau)
        {
            if (tau < 1e-10) return S > K ? 10 : -10;
            return (Math.Log(S / K) + (_r - _q + 0.5 * _sigma * _sigma) * tau) / (_sigma * Math.Sqrt(tau));
        }
        
        private double CDF(double x)
        {
            if (x > 8) return 1.0;
            if (x < -8) return 0.0;
            return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
        }
        
        private double PDF(double x)
        {
            return Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
        }
        
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
        
        private double Alpha => 2 * _r / (_sigma * _sigma);
        private double Beta => 2 * (_r - _q) / (_sigma * _sigma);
    }
    
    /// <summary>
    /// Result container for boundary computation.
    /// </summary>
    public class BoundaryResult
    {
        public double[] UpperBoundary { get; set; } = Array.Empty<double>();
        public double[] LowerBoundary { get; set; } = Array.Empty<double>();
        public double CrossingTime { get; set; }
    }
}