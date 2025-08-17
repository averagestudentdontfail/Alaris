// Alaris.Strategy/Core/YangZhangEstimator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;

namespace Alaris.Strategy.Core
{
    /// <summary>
    /// Yang-Zhang volatility estimator implementation
    /// Based on Yang & Zhang (2000) drift-independent volatility estimation
    /// </summary>
    public class YangZhangEstimator
    {
        private const int TRADING_DAYS_PER_YEAR = 252;
        
        public class PriceBar
        {
            public DateTime Date { get; set; }
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
            public double Volume { get; set; }
        }
        
        /// <summary>
        /// Calculate Yang-Zhang realized volatility
        /// σ²_YZ = σ²_o + k·σ²_c + (1-k)·σ²_rs
        /// </summary>
        public double Calculate(List<PriceBar> priceBars, int window = 30)
        {
            if (priceBars.Count < window + 1)
                throw new ArgumentException($"Insufficient data. Need at least {window + 1} bars.");
            
            var recentBars = priceBars.TakeLast(window + 1).ToList();
            
            // Calculate overnight variance (σ²_o)
            var overnightVar = CalculateOvernightVariance(recentBars);
            
            // Calculate close-to-close variance (σ²_c)
            var closeToCloseVar = CalculateCloseToCloseVariance(recentBars);
            
            // Calculate Rogers-Satchell variance (σ²_rs)
            var rogersSatchellVar = CalculateRogersSatchellVariance(recentBars);
            
            // Calculate optimal weighting factor k
            double k = 0.34 / (1.34 + ((double)(window + 1) / (window - 1)));
            
            // Combine components
            double yangZhangVar = overnightVar + k * closeToCloseVar + (1 - k) * rogersSatchellVar;
            
            // Convert to annualized volatility
            return Math.Sqrt(yangZhangVar * TRADING_DAYS_PER_YEAR);
        }
        
        private double CalculateOvernightVariance(List<PriceBar> bars)
        {
            var overnightReturns = new List<double>();
            
            for (int i = 1; i < bars.Count; i++)
            {
                double overnightReturn = Math.Log(bars[i].Open / bars[i - 1].Close);
                overnightReturns.Add(overnightReturn);
            }
            
            double mean = overnightReturns.Average();
            double sumSquaredDeviations = overnightReturns.Sum(r => Math.Pow(r - mean, 2));
            
            return sumSquaredDeviations / (overnightReturns.Count - 1);
        }
        
        private double CalculateCloseToCloseVariance(List<PriceBar> bars)
        {
            var closeReturns = new List<double>();
            
            for (int i = 1; i < bars.Count; i++)
            {
                double closeReturn = Math.Log(bars[i].Close / bars[i - 1].Close);
                closeReturns.Add(closeReturn);
            }
            
            double mean = closeReturns.Average();
            double sumSquaredDeviations = closeReturns.Sum(r => Math.Pow(r - mean, 2));
            
            return sumSquaredDeviations / (closeReturns.Count - 1);
        }
        
        private double CalculateRogersSatchellVariance(List<PriceBar> bars)
        {
            double sum = 0;
            int count = 0;
            
            for (int i = 1; i < bars.Count; i++)
            {
                double u_i = Math.Log(bars[i].High / bars[i].Open);
                double d_i = Math.Log(bars[i].Low / bars[i].Open);
                double c_i = Math.Log(bars[i].Close / bars[i].Open);
                
                double rs_i = u_i * (u_i - c_i) + d_i * (d_i - c_i);
                sum += rs_i;
                count++;
            }
            
            return sum / count;
        }
    }
}