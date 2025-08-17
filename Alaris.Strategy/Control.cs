// Alaris.Strategy/Control.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Alaris.Strategy
{
    public class Control
    {
        private readonly SunnySignalGenerator _signalGenerator;
        private readonly AlarisPricingIntegration _pricingEngine;
        private readonly KellyPositionSizer _positionSizer;
        private readonly ILogger<Control> _logger;
        
        public async Task<List<TradingRecommendation>> ScanForOpportunities(
            List<string> symbols,
            Dictionary<string, DateTime> earningsDates)
        {
            var recommendations = new List<TradingRecommendation>();
            
            foreach (var symbol in symbols)
            {
                try
                {
                    // Generate signal
                    var signal = await _signalGenerator.GenerateSignal(
                        symbol,
                        earningsDates.GetValueOrDefault(symbol));
                    
                    if (signal.Strength == SignalStrength.Avoid)
                        continue;
                    
                    // Price the calendar spread
                    var pricing = await _pricingEngine.PriceCalendarSpread(
                        new CalendarSpreadParameters
                        {
                            UnderlyingPrice = signal.OptimalStrike,
                            Strike = signal.OptimalStrike,
                            ImpliedVolatility = signal.IV30,
                            // ... other parameters
                        });
                    
                    // Calculate position size
                    var positionSize = _positionSizer.CalculateFromHistory(
                        portfolioValue: 100000, // Example
                        historicalTrades: new List<TradeResult>(),
                        spreadCost: pricing.SpreadCost,
                        signal: signal);
                    
                    recommendations.Add(new TradingRecommendation
                    {
                        Signal = signal,
                        Pricing = pricing,
                        PositionSize = positionSize,
                        ExpectedPnL = CalculateExpectedPnL(signal, pricing, positionSize),
                        RiskMetrics = CalculateRiskMetrics(signal, pricing, positionSize)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing {symbol}");
                }
            }
            
            // Rank by expected value
            return recommendations
                .OrderByDescending(r => r.ExpectedPnL.ExpectedValue)
                .ToList();
        }
        
        private ExpectedPnL CalculateExpectedPnL(
            SunnySignal signal,
            CalendarSpreadPricing pricing,
            PositionSize position)
        {
            // Historical win rate for similar setups
            double winRate = 0.68; // Example from backtesting
            
            // Expected profit if successful (IV crush scenario)
            double expectedProfit = pricing.MaxProfit * 0.25; // Conservative 25% of max
            
            // Expected loss if unsuccessful  
            double expectedLoss = pricing.MaxLoss * 0.5; // Typical 50% stop loss
            
            return new ExpectedPnL
            {
                ExpectedValue = winRate * expectedProfit * position.Contracts - 
                               (1 - winRate) * expectedLoss * position.Contracts,
                WinScenario = expectedProfit * position.Contracts,
                LossScenario = -expectedLoss * position.Contracts,
                Probability = winRate
            };
        }
        
        private RiskMetrics CalculateRiskMetrics(
            SunnySignal signal,
            CalendarSpreadPricing pricing,
            PositionSize position)
        {
            return new RiskMetrics
            {
                MaxDrawdown = pricing.MaxLoss * position.Contracts,
                DeltaExposure = pricing.SpreadDelta * position.Contracts * 100,
                GammaExposure = pricing.SpreadGamma * position.Contracts * 100,
                VegaExposure = pricing.SpreadVega * position.Contracts * 100,
                ThetaDecay = pricing.SpreadTheta * position.Contracts * 100,
                StressTestLoss = CalculateStressLoss(pricing, position)
            };
        }
        
        private double CalculateStressLoss(CalendarSpreadPricing pricing, PositionSize position)
        {
            // Stress scenario: 2 standard deviation move + 50% IV spike
            return pricing.MaxLoss * 1.5 * position.Contracts;
        }
    }
    
    public class TradingRecommendation
    {
        public SunnySignal Signal { get; set; }
        public CalendarSpreadPricing Pricing { get; set; }
        public PositionSize PositionSize { get; set; }
        public ExpectedPnL ExpectedPnL { get; set; }
        public RiskMetrics RiskMetrics { get; set; }
    }
    
    public class ExpectedPnL
    {
        public double ExpectedValue { get; set; }
        public double WinScenario { get; set; }
        public double LossScenario { get; set; }
        public double Probability { get; set; }
    }
    
    public class RiskMetrics
    {
        public double MaxDrawdown { get; set; }
        public double DeltaExposure { get; set; }
        public double GammaExposure { get; set; }
        public double VegaExposure { get; set; }
        public double ThetaDecay { get; set; }
        public double StressTestLoss { get; set; }
    }
}