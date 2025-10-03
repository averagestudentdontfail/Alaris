// Alaris.Double/DoubleBoundarySolver.cs
using System;

namespace Alaris.Double
{
    /// <summary>
    /// Solves the double boundary integral equation using the FP-B' fixed-point iteration.
    /// 
    /// The key innovation (Healy Equation 33-35) is that the lower boundary calculation
    /// uses the UPDATED upper boundary, providing stability that the standard FP-B lacks.
    /// 
    /// Reference: Healy Section 5.3
    /// </summary>
    public class DoubleBoundarySolver
    {
        private readonly GeneralizedBlackScholesProcess _process;
        private readonly double _K, _T, _r, _q, _sigma;
        private readonly QdFpIterationScheme _scheme;
        
        private const double CONVERGENCE_TOLERANCE = 1e-6;
        private const int MAX_ITERATIONS = 32;
        private const int QUADRATURE_POINTS = 64;
        
        public DoubleBoundarySolver(
            GeneralizedBlackScholesProcess process,
            double K, double T, double r, double q, double sigma,
            QdFpIterationScheme scheme)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _K = K;
            _T = T;
            _r = r;
            _q = q;
            _sigma = sigma;
            _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
        }
        
        /// <summary>
        /// Solves for refined boundaries using FP-B' iteration.
        /// </summary>
        /// <param name="initialUpper">Initial upper boundary estimate</param>
        /// <param name="initialLower">Initial lower boundary estimate</param>
        /// <param name="crossingTime">Time when boundaries cross</param>
        /// <returns>Refined boundary result</returns>
        public BoundaryResult Solve(double[] initialUpper, double[] initialLower, double crossingTime)
        {
            int m = initialUpper.Length;
            double[] upper = (double[])initialUpper.Clone();
            double[] lower = (double[])initialLower.Clone();
            
            // FP-B' iteration - critical stability modification
            for (int iter = 0; iter < MAX_ITERATIONS; iter++)
            {
                double[] upperNew = new double[m];
                double[] lowerNew = new double[m];
                double maxChange = 0;
                
                for (int i = 0; i < m; i++)
                {
                    double t = i * _T / (m - 1);
                    double tau = _T - t;
                    
                    // Before crossing, boundaries are equal
                    if (t < crossingTime)
                    {
                        double c_star = Math.Min(upper[i], lower[i]);
                        upperNew[i] = c_star;
                        lowerNew[i] = c_star;
                        continue;
                    }
                    
                    // Update upper boundary (standard FP-B)
                    upperNew[i] = _K * Numerator(tau, upper[i], upper, lower, crossingTime) /
                                      Denominator(tau, upper[i], upper, lower, crossingTime);
                    
                    // Update lower boundary (FP-B' with updated upper!)
                    // This is the critical modification from Healy Section 5.3
                    lowerNew[i] = _K * NumeratorPrime(tau, lower[i], upperNew, lower, crossingTime) /
                                      DenominatorPrime(tau, lower[i], upperNew, lower, crossingTime);
                    
                    // Track convergence
                    double changeUpper = Math.Abs(upperNew[i] - upper[i]) / Math.Max(upper[i], 1e-10);
                    double changeLower = Math.Abs(lowerNew[i] - lower[i]) / Math.Max(lower[i], 1e-10);
                    maxChange = Math.Max(maxChange, Math.Max(changeUpper, changeLower));
                }
                
                upper = upperNew;
                lower = lowerNew;
                
                // Check convergence
                if (maxChange < CONVERGENCE_TOLERANCE)
                {
                    Console.WriteLine($"FP-B' converged in {iter + 1} iterations (max change: {maxChange:E2})");
                    break;
                }
                
                if (iter == MAX_ITERATIONS - 1)
                {
                    Console.WriteLine($"FP-B' reached max iterations (residual: {maxChange:E2})");
                }
            }
            
            return new BoundaryResult
            {
                UpperBoundary = upper,
                LowerBoundary = lower,
                CrossingTime = crossingTime
            };
        }
        
        private double Numerator(double tau, double B_tau, double[] upper, double[] lower, double ts)
        {
            // Healy Equation 31 - Standard FP-B numerator
            double d2_euro = D2(B_tau, _K, tau);
            double N_euro = 1 - Math.Exp(-_r * tau) * CDF(-d2_euro);
            double integral = IntegrateNumeratorTerm(tau, B_tau, upper, lower, ts);
            return N_euro - integral;
        }
        
        private double Denominator(double tau, double B_tau, double[] upper, double[] lower, double ts)
        {
            // Healy Equation 32 - Standard FP-B denominator
            double d1_euro = D1(B_tau, _K, tau);
            double D_euro = 1 - Math.Exp(-_q * tau) * CDF(-d1_euro);
            double integral = IntegrateDenominatorTerm(tau, B_tau, upper, lower, ts);
            return D_euro - integral;
        }
        
        private double NumeratorPrime(double tau, double B_tau, double[] upper, double[] lower, double ts)
        {
            // Healy Equation 34 - Modified numerator for FP-B'
            double d2_euro = D2(B_tau, _K, tau);
            double N_euro = 1 - Math.Exp(-_r * tau) * CDF(-d2_euro);
            double integral_N = IntegrateNumeratorTerm(tau, B_tau, upper, lower, ts);
            
            // Additional symmetry term
            double integral_D = IntegrateDenominatorTerm(tau, B_tau, upper, lower, ts);
            double symmetryTerm = (B_tau / _K) * integral_D;
            
            return N_euro - integral_N + symmetryTerm;
        }
        
        private double DenominatorPrime(double tau, double B_tau, double[] upper, double[] lower, double ts)
        {
            // Healy Equation 35 - Simplified denominator for FP-B'
            double d1_euro = D1(B_tau, _K, tau);
            return 1 - Math.Exp(-_q * tau) * CDF(-d1_euro);
        }
        
        private double IntegrateNumeratorTerm(double tau, double B_tau, double[] upper, double[] lower, double ts)
        {
            // Integrate: r * exp(-r*(t-ti)) * [Phi(-d2(B,u)) - Phi(-d2(B,l))]
            double t_i = _T - tau;
            double t_start = Math.Max(t_i, ts);
            
            if (t_start >= _T)
                return 0;
            
            double dt = (_T - t_start) / QUADRATURE_POINTS;
            double integral = 0;
            
            for (int j = 0; j < QUADRATURE_POINTS; j++)
            {
                double t = t_start + (j + 0.5) * dt;
                double t_minus_ti = t - t_i;
                
                if (t_minus_ti < 1e-10) continue;
                
                double u_t = Interpolate(upper, t);
                double l_t = Interpolate(lower, t);
                
                double d2_u = D2(B_tau, u_t, t_minus_ti);
                double d2_l = D2(B_tau, l_t, t_minus_ti);
                
                double term = _r * Math.Exp(-_r * t_minus_ti) * 
                            (CDF(-d2_u) - CDF(-d2_l));
                
                integral += term * dt;
            }
            
            return integral;
        }
        
        private double IntegrateDenominatorTerm(double tau, double B_tau, double[] upper, double[] lower, double ts)
        {
            // Integrate: q * exp(-q*(t-ti)) * [Phi(-d1(B,u)) - Phi(-d1(B,l))]
            double t_i = _T - tau;
            double t_start = Math.Max(t_i, ts);
            
            if (t_start >= _T)
                return 0;
            
            double dt = (_T - t_start) / QUADRATURE_POINTS;
            double integral = 0;
            
            for (int j = 0; j < QUADRATURE_POINTS; j++)
            {
                double t = t_start + (j + 0.5) * dt;
                double t_minus_ti = t - t_i;
                
                if (t_minus_ti < 1e-10) continue;
                
                double u_t = Interpolate(upper, t);
                double l_t = Interpolate(lower, t);
                
                double d1_u = D1(B_tau, u_t, t_minus_ti);
                double d1_l = D1(B_tau, l_t, t_minus_ti);
                
                double term = _q * Math.Exp(-_q * t_minus_ti) * 
                            (CDF(-d1_u) - CDF(-d1_l));
                
                integral += term * dt;
            }
            
            return integral;
        }
        
        private double Interpolate(double[] boundary, double t)
        {
            int m = boundary.Length;
            double dt = _T / (m - 1);
            int i = (int)(t / dt);
            
            if (i >= m - 1) return boundary[m - 1];
            if (i < 0) return boundary[0];
            
            double alpha = (t - i * dt) / dt;
            return boundary[i] * (1 - alpha) + boundary[i + 1] * alpha;
        }
        
        private double D1(double S, double K, double tau)
        {
            if (tau < 1e-10) return S > K ? 10 : -10;
            return (Math.Log(S / K) + (_r - _q + 0.5 * _sigma * _sigma) * tau) / (_sigma * Math.Sqrt(tau));
        }
        
        private double D2(double S, double K, double tau)
        {
            if (tau < 1e-10) return S > K ? 10 : -10;
            return (Math.Log(S / K) + (_r - _q - 0.5 * _sigma * _sigma) * tau) / (_sigma * Math.Sqrt(tau));
        }
        
        private double CDF(double x)
        {
            if (x > 8) return 1.0;
            if (x < -8) return 0.0;
            return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
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
    }
}