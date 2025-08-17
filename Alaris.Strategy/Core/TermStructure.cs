// Alaris.Strategy/Core/TermStructure.cs
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;

namespace Alaris.Strategy.Core
{
    /// <summary>
    /// Analyzes implied volatility term structure for backwardation signals
    /// </summary>
    public class TermStructure
    {
        public class TermStructurePoint
        {
            public int DaysToExpiry { get; set; }
            public double ImpliedVolatility { get; set; }
            public double Strike { get; set; }
            public DateTime ExpiryDate { get; set; }
        }
        
        public class TermStructureAnalysis
        {
            public double Slope { get; set; }
            public double Intercept { get; set; }
            public double RSquared { get; set; }
            public double StandardError { get; set; }
            public bool IsBackwardated => Slope <= -0.00406;
            public List<TermStructurePoint> DataPoints { get; set; }
        }
        
        /// <summary>
        /// Perform OLS regression on IV term structure
        /// IV_i = β₀ + β₁ · DTE_i + ε_i
        /// </summary>
        public TermStructureAnalysis Analyze(List<TermStructurePoint> points)
        {
            if (points.Count < 3)
                throw new ArgumentException("Need at least 3 points for term structure analysis");
            
            // Extract arrays for regression
            var x = points.Select(p => (double)p.DaysToExpiry).ToArray();
            var y = points.Select(p => p.ImpliedVolatility).ToArray();
            
            // Calculate regression coefficients
            var (intercept, slope) = Fit.Line(x, y);
            
            // Calculate R-squared
            double yMean = y.Average();
            double totalSS = y.Sum(yi => Math.Pow(yi - yMean, 2));
            double residualSS = points.Sum(p => 
            {
                double predicted = intercept + slope * p.DaysToExpiry;
                return Math.Pow(p.ImpliedVolatility - predicted, 2);
            });
            double rSquared = 1 - (residualSS / totalSS);
            
            // Calculate standard error
            double standardError = Math.Sqrt(residualSS / (points.Count - 2));
            
            return new TermStructureAnalysis
            {
                Slope = slope,
                Intercept = intercept,
                RSquared = rSquared,
                StandardError = standardError,
                DataPoints = points
            };
        }
        
        /// <summary>
        /// Interpolate IV at specific DTE using cubic spline
        /// </summary>
        public double InterpolateIV(List<TermStructurePoint> points, int targetDTE)
        {
            if (points.Count < 2)
                throw new ArgumentException("Need at least 2 points for interpolation");
            
            var sortedPoints = points.OrderBy(p => p.DaysToExpiry).ToList();
            
            // If target is outside range, use linear extrapolation
            if (targetDTE <= sortedPoints.First().DaysToExpiry)
                return sortedPoints.First().ImpliedVolatility;
            
            if (targetDTE >= sortedPoints.Last().DaysToExpiry)
                return sortedPoints.Last().ImpliedVolatility;
            
            // Find bracketing points
            var lower = sortedPoints.LastOrDefault(p => p.DaysToExpiry <= targetDTE);
            var upper = sortedPoints.FirstOrDefault(p => p.DaysToExpiry > targetDTE);
            
            if (lower == null || upper == null)
                return sortedPoints.First().ImpliedVolatility;
            
            // Linear interpolation
            double weight = (targetDTE - lower.DaysToExpiry) / 
                          (double)(upper.DaysToExpiry - lower.DaysToExpiry);
            
            return lower.ImpliedVolatility * (1 - weight) + upper.ImpliedVolatility * weight;
        }
    }
}