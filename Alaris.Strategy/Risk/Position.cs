// Alaris.Strategy/Risk/Position.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alaris.Strategy.Risk
{
    public class KellyPositionSizer
    {
        private readonly double _kellyFraction;
        private readonly double _maxPositionSize;
        private readonly double _minPositionSize;
        
        public KellyPositionSizer(
            double kellyFraction = 0.25,  // Fractional Kelly
            double maxPositionSize = 0.06, // 6% max per position
            double minPositionSize = 0.01) // 1% min per position
        {
            _kellyFraction = kellyFraction;
            _maxPositionSize = maxPositionSize;
            _minPositionSize = minPositionSize;
        }
        
        /// <summary>
        /// Calculate optimal position size using Kelly Criterion
        /// f* = (p·b - q) / b
        /// where p = win probability, q = loss probability, b = win/loss ratio
        /// </summary>
        public PositionSize Calculate(
            double portfolioValue,
            double winProbability,
            double averageWin,
            double averageLoss,
            double spreadCost,
            double confidence = 1.0)
        {
            // Calculate Kelly optimal fraction
            double b = Math.Abs(averageWin / averageLoss);
            double q = 1 - winProbability;
            double kellyOptimal = (winProbability * b - q) / b;
            
            // Apply fractional Kelly for safety
            double adjustedKelly = kellyOptimal * _kellyFraction * confidence;
            
            // Constrain to position limits
            double positionFraction = Math.Max(_minPositionSize, 
                                              Math.Min(_maxPositionSize, adjustedKelly));
            
            // Calculate number of contracts
            double allocation = portfolioValue * positionFraction;
            int contracts = (int)Math.Floor(allocation / (spreadCost * 100));
            
            return new PositionSize
            {
                Contracts = contracts,
                AllocationPercent = positionFraction,
                DollarAllocation = contracts * spreadCost * 100,
                KellyFraction = kellyOptimal,
                AdjustedKellyFraction = adjustedKelly,
                Confidence = confidence
            };
        }
        
        /// <summary>
        /// Calculate position size based on historical performance
        /// </summary>
        public PositionSize CalculateFromHistory(
            double portfolioValue,
            List<TradeResult> historicalTrades,
            double spreadCost,
            Signal signal)
        {
            if (historicalTrades.Count < 30)
            {
                // Use conservative sizing for insufficient history
                return CalculateDefault(portfolioValue, spreadCost);
            }
            
            // Calculate win rate and average win/loss
            var wins = historicalTrades.Where(t => t.PnL > 0).ToList();
            var losses = historicalTrades.Where(t => t.PnL <= 0).ToList();
            
            double winRate = (double)wins.Count / historicalTrades.Count;
            double avgWin = wins.Any() ? wins.Average(w => w.PnL) : 0;
            double avgLoss = losses.Any() ? Math.Abs(losses.Average(l => l.PnL)) : spreadCost;
            
            // Adjust confidence based on signal strength
            double confidence = signal.Strength switch
            {
                SignalStrength.Recommended => 1.0,
                SignalStrength.Consider => 0.5,
                _ => 0.0
            };
            
            // Additional confidence adjustment based on IV/RV ratio
            if (signal.IVRVRatio > 1.5)
                confidence *= 1.2;
            else if (signal.IVRVRatio < 1.3)
                confidence *= 0.8;
            
            return Calculate(portfolioValue, winRate, avgWin, avgLoss, spreadCost, confidence);
        }
        
        private PositionSize CalculateDefault(double portfolioValue, double spreadCost)
        {
            // Default conservative sizing
            double allocation = portfolioValue * 0.02; // 2% default
            int contracts = (int)Math.Floor(allocation / (spreadCost * 100));
            
            return new PositionSize
            {
                Contracts = contracts,
                AllocationPercent = 0.02,
                DollarAllocation = contracts * spreadCost * 100,
                KellyFraction = 0.0,
                AdjustedKellyFraction = 0.0,
                Confidence = 0.5
            };
        }
    }
    
    public class PositionSize
    {
        public int Contracts { get; set; }
        public double AllocationPercent { get; set; }
        public double DollarAllocation { get; set; }
        public double KellyFraction { get; set; }
        public double AdjustedKellyFraction { get; set; }
        public double Confidence { get; set; }
    }
    
    public class TradeResult
    {
        public string Symbol { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public double PnL { get; set; }
        public double ReturnPercent { get; set; }
    }
}