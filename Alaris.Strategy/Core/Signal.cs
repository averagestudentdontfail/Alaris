// Alaris.Strategy/Core/Signal.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Alaris.Strategy.Core
{
    public enum SignalStrength
    {
        Avoid = 0,
        Consider = 1,
        Recommended = 2
    }
    
    public class Signal
    {
        public string Symbol { get; set; }
        public SignalStrength Strength { get; set; }
        public double IVRVRatio { get; set; }
        public double TermStructureSlope { get; set; }
        public double AverageVolume { get; set; }
        public double IV30 { get; set; }
        public double RV30 { get; set; }
        public DateTime EarningsDate { get; set; }
        public double OptimalStrike { get; set; }
        public double ExpectedMove { get; set; }
        public Dictionary<string, bool> Criteria { get; set; }
        public DateTime SignalGeneratedAt { get; set; }
    }
    
    public class SignalGenerator
    {
        private readonly YangZhangEstimator _yangZhang;
        private readonly TermStructure _termStructure;
        private readonly IMarketDataProvider _marketData;
        private readonly IOptionPricingEngine _pricingEngine;
        
        // Algorithm thresholds
        private const double MIN_VOLUME = 1_500_000;
        private const double MIN_IV_RV_RATIO = 1.25;
        private const double MAX_TERM_SLOPE = -0.00406;
        
        public SignalGenerator(
            YangZhangEstimator yangZhang,
            TermStructure termStructure,
            IMarketDataProvider marketData,
            IOptionPricingEngine pricingEngine)
        {
            _yangZhang = yangZhang;
            _termStructure = termStructure;
            _marketData = marketData;
            _pricingEngine = pricingEngine;
        }
        
        public async Task<Signal> GenerateSignal(string symbol, DateTime? earningsDate = null)
        {
            // 1. Get historical price data for RV calculation
            var priceHistory = await _marketData.GetPriceHistory(symbol, 90);
            var rv30 = _yangZhang.Calculate(priceHistory, 30);
            
            // 2. Get option chain for IV analysis
            var optionChain = await _marketData.GetOptionChain(symbol);
            var underlyingPrice = await _marketData.GetCurrentPrice(symbol);
            
            // 3. Extract ATM IV term structure
            var atmPoints = ExtractATMTermStructure(optionChain, underlyingPrice);
            var termAnalysis = _termStructure.Analyze(atmPoints);
            
            // 4. Calculate IV30 through interpolation
            var iv30 = _termStructure.InterpolateIV(atmPoints, 30);
            
            // 5. Calculate IV/RV ratio
            var ivRvRatio = iv30 / rv30;
            
            // 6. Get average volume
            var avgVolume = priceHistory.TakeLast(30).Average(p => p.Volume);
            
            // 7. Calculate expected move from front-month straddle
            var expectedMove = CalculateExpectedMove(optionChain, underlyingPrice);
            
            // 8. Evaluate criteria
            var criteria = new Dictionary<string, bool>
            {
                ["Volume"] = avgVolume >= MIN_VOLUME,
                ["IV/RV"] = ivRvRatio >= MIN_IV_RV_RATIO,
                ["TermSlope"] = termAnalysis.Slope <= MAX_TERM_SLOPE
            };
            
            // 9. Determine signal strength
            var strength = DetermineSignalStrength(criteria);
            
            return new Signal
            {
                Symbol = symbol,
                Strength = strength,
                IVRVRatio = ivRvRatio,
                TermStructureSlope = termAnalysis.Slope,
                AverageVolume = avgVolume,
                IV30 = iv30,
                RV30 = rv30,
                EarningsDate = earningsDate ?? DateTime.MinValue,
                OptimalStrike = SelectOptimalStrike(optionChain, underlyingPrice),
                ExpectedMove = expectedMove,
                Criteria = criteria,
                SignalGeneratedAt = DateTime.UtcNow
            };
        }
        
        private List<TermStructureAnalyzer.TermStructurePoint> ExtractATMTermStructure(
            OptionChain chain, double underlyingPrice)
        {
            var points = new List<TermStructureAnalyzer.TermStructurePoint>();
            
            foreach (var expiry in chain.Expiries)
            {
                // Find ATM options
                var atmCall = expiry.Calls
                    .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
                    .FirstOrDefault();
                
                var atmPut = expiry.Puts
                    .OrderBy(p => Math.Abs(p.Strike - underlyingPrice))
                    .FirstOrDefault();
                
                if (atmCall != null && atmPut != null)
                {
                    // Average call and put IV for ATM
                    double atmIV = (atmCall.ImpliedVolatility + atmPut.ImpliedVolatility) / 2.0;
                    
                    points.Add(new TermStructureAnalyzer.TermStructurePoint
                    {
                        DaysToExpiry = (expiry.ExpiryDate - DateTime.Today).Days,
                        ImpliedVolatility = atmIV,
                        Strike = atmCall.Strike,
                        ExpiryDate = expiry.ExpiryDate
                    });
                }
            }
            
            return points;
        }
        
        private SignalStrength DetermineSignalStrength(Dictionary<string, bool> criteria)
        {
            bool volume = criteria["Volume"];
            bool ivRv = criteria["IV/RV"];
            bool slope = criteria["TermSlope"];
            
            // RECOMMENDED: All three criteria met
            if (volume && ivRv && slope)
                return SignalStrength.Recommended;
            
            // CONSIDER: Slope is good and one other criterion
            if (slope && (volume || ivRv))
                return SignalStrength.Consider;
            
            // AVOID: Otherwise
            return SignalStrength.Avoid;
        }
        
        private double CalculateExpectedMove(OptionChain chain, double underlyingPrice)
        {
            // Get front-month expiry
            var frontMonth = chain.Expiries
                .OrderBy(e => e.ExpiryDate)
                .FirstOrDefault();
            
            if (frontMonth == null)
                return 0;
            
            // Find ATM straddle
            var atmStrike = frontMonth.Calls
                .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
                .FirstOrDefault()?.Strike ?? underlyingPrice;
            
            var call = frontMonth.Calls.FirstOrDefault(c => c.Strike == atmStrike);
            var put = frontMonth.Puts.FirstOrDefault(p => p.Strike == atmStrike);
            
            if (call == null || put == null)
                return 0;
            
            // Calculate straddle price
            double callMid = (call.Bid + call.Ask) / 2.0;
            double putMid = (put.Bid + put.Ask) / 2.0;
            double straddlePrice = callMid + putMid;
            
            // Expected move as percentage
            return (straddlePrice / underlyingPrice) * 100;
        }
        
        private double SelectOptimalStrike(OptionChain chain, double underlyingPrice)
        {
            // Select ATM strike with highest gamma (maximum time decay)
            var frontMonth = chain.Expiries
                .OrderBy(e => e.ExpiryDate)
                .FirstOrDefault();
            
            if (frontMonth == null)
                return underlyingPrice;
            
            // Round to nearest standard strike
            var strikes = frontMonth.Calls.Select(c => c.Strike).Distinct().OrderBy(s => s).ToList();
            
            return strikes.OrderBy(s => Math.Abs(s - underlyingPrice)).FirstOrDefault();
        }
    }
}