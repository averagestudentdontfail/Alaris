// Alaris.Tests/Integration/StrategyPricingIntegrationTest.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Risk;
using Alaris.Quantlib;

namespace Alaris.Tests.Integration
{
    public class StrategyPricingIntegrationTest
    {
        [Fact]
        public async Task TestCompleteAlarisWorkflow()
        {
            // ============================================
            // STEP 1: Setup Market Data
            // ============================================
            var testSymbol = "AAPL";
            var currentDate = new Date(15, Month.March, 2024);
            var earningsDate = new DateTime(2024, 4, 25); // Earnings in ~40 days
            
            // Create fictitious price history for Yang-Zhang calculation
            var priceHistory = GeneratePriceHistory(
                basePrice: 150.0,
                volatility: 0.25,
                days: 90
            );
            
            // Create fictitious option chain with earnings vol premium
            var optionChain = GenerateOptionChain(
                underlyingPrice: 150.0,
                earningsDate: earningsDate,
                currentDate: DateTime.Now
            );
            
            // ============================================
            // STEP 2: Calculate Yang-Zhang Realized Volatility
            // ============================================
            var yangZhangEstimator = new YangZhangEstimator();
            var realizedVol = yangZhangEstimator.Calculate(priceHistory, 30);
            
            Console.WriteLine($"30-Day Yang-Zhang RV: {realizedVol:P2}");
            Assert.InRange(realizedVol, 0.15, 0.35); // Reasonable range for equity
            
            // ============================================
            // STEP 3: Analyze Term Structure
            // ============================================
            var termStructure = new TermStructure();
            var termPoints = ExtractTermStructurePoints(optionChain, 150.0);
            var termAnalysis = termStructure.Analyze(termPoints);
            
            Console.WriteLine($"Term Structure Slope: {termAnalysis.Slope:F6}");
            Console.WriteLine($"Is Backwardated: {termAnalysis.IsBackwardated}");
            
            // For earnings play, we expect backwardation
            Assert.True(termAnalysis.IsBackwardated);
            Assert.True(termAnalysis.Slope <= -0.00406);
            
            // ============================================
            // STEP 4: Generate Trading Signal
            // ============================================
            var marketDataMock = new MockMarketDataProvider(priceHistory, optionChain, 150.0);
            var pricingEngineMock = new MockPricingEngine();
            
            var signalGenerator = new SignalGenerator(
                yangZhangEstimator,
                termStructure,
                marketDataMock,
                pricingEngineMock
            );
            
            var signal = await signalGenerator.GenerateSignal(testSymbol, earningsDate);
            
            Console.WriteLine($"Signal Strength: {signal.Strength}");
            Console.WriteLine($"IV/RV Ratio: {signal.IVRVRatio:F2}");
            Console.WriteLine($"Expected Move: {signal.ExpectedMove:F2}%");
            
            // Verify signal criteria
            Assert.Equal(SignalStrength.Recommended, signal.Strength);
            Assert.True(signal.IVRVRatio >= 1.25);
            Assert.True(signal.Criteria["Volume"]);
            Assert.True(signal.Criteria["IV/RV"]);
            Assert.True(signal.Criteria["TermSlope"]);
            
            // ============================================
            // STEP 5: Price Calendar Spread with Alaris
            // ============================================
            var bridge = new Bridge();
            
            var spreadParams = new CalendarSpreadParameters
            {
                UnderlyingPrice = 150.0,
                Strike = 150.0, // ATM
                FrontExpiry = new Date(26, Month.April, 2024), // 1 day after earnings
                BackExpiry = new Date(17, Month.May, 2024), // ~30 days later
                ImpliedVolatility = signal.IV30,
                RiskFreeRate = 0.045, // 4.5% risk-free rate
                DividendYield = 0.015, // 1.5% dividend yield
                OptionType = Option.Type.Call,
                ValuationDate = currentDate
            };
            
            var calendarPricing = await bridge.PriceCalendarSpread(spreadParams);
            
            Console.WriteLine("\n=== Calendar Spread Pricing ===");
            Console.WriteLine($"Front Option (Short): ${calendarPricing.FrontOption.Price:F2}");
            Console.WriteLine($"Back Option (Long): ${calendarPricing.BackOption.Price:F2}");
            Console.WriteLine($"Net Debit: ${calendarPricing.SpreadCost:F2}");
            Console.WriteLine($"Spread Delta: {calendarPricing.SpreadDelta:F4}");
            Console.WriteLine($"Spread Vega: {calendarPricing.SpreadVega:F4}");
            Console.WriteLine($"Spread Theta: {calendarPricing.SpreadTheta:F4}");
            Console.WriteLine($"Max Profit: ${calendarPricing.MaxProfit:F2}");
            Console.WriteLine($"Max Loss: ${calendarPricing.MaxLoss:F2}");
            
            // Verify pricing relationships
            Assert.True(calendarPricing.BackOption.Price > calendarPricing.FrontOption.Price);
            Assert.True(calendarPricing.SpreadCost > 0);
            Assert.InRange(Math.Abs(calendarPricing.SpreadDelta), 0, 0.1); // Near delta-neutral
            Assert.True(calendarPricing.SpreadVega < 0); // Short vega (benefits from IV decline)
            Assert.True(calendarPricing.SpreadTheta > 0); // Positive theta
            
            // ============================================
            // STEP 6: Calculate Position Size with Kelly
            // ============================================
            var positionSizer = new KellyPositionSizer(
                kellyFraction: 0.25,
                maxPositionSize: 0.06,
                minPositionSize: 0.01
            );
            
            var portfolioValue = 100000.0; // $100k portfolio
            
            // Create historical trade results for Kelly calculation
            var historicalTrades = GenerateHistoricalTrades();
            
            var positionSize = positionSizer.CalculateFromHistory(
                portfolioValue,
                historicalTrades,
                calendarPricing.SpreadCost,
                signal
            );
            
            Console.WriteLine("\n=== Position Sizing (Kelly) ===");
            Console.WriteLine($"Contracts: {positionSize.Contracts}");
            Console.WriteLine($"Dollar Allocation: ${positionSize.DollarAllocation:F2}");
            Console.WriteLine($"Portfolio %: {positionSize.AllocationPercent:P2}");
            Console.WriteLine($"Kelly Fraction: {positionSize.KellyFraction:F4}");
            Console.WriteLine($"Adjusted Kelly: {positionSize.AdjustedKellyFraction:F4}");
            Console.WriteLine($"Confidence: {positionSize.Confidence:F2}");
            
            // Verify position sizing constraints
            Assert.True(positionSize.Contracts > 0);
            Assert.InRange(positionSize.AllocationPercent, 0.01, 0.06);
            Assert.True(positionSize.DollarAllocation <= portfolioValue * 0.06);
            
            // ============================================
            // STEP 7: Risk Metrics Validation
            // ============================================
            var portfolioDelta = calendarPricing.SpreadDelta * positionSize.Contracts * 100;
            var portfolioVega = calendarPricing.SpreadVega * positionSize.Contracts * 100;
            var portfolioTheta = calendarPricing.SpreadTheta * positionSize.Contracts * 100;
            
            Console.WriteLine("\n=== Portfolio Risk Metrics ===");
            Console.WriteLine($"Portfolio Delta: ${portfolioDelta:F2}");
            Console.WriteLine($"Portfolio Vega: ${portfolioVega:F2}");
            Console.WriteLine($"Portfolio Theta: ${portfolioTheta:F2}");
            Console.WriteLine($"Max Risk: ${positionSize.DollarAllocation:F2}");
            
            // Risk limits validation
            Assert.True(Math.Abs(portfolioDelta) < portfolioValue * 0.05); // Delta < 5% of portfolio
            Assert.True(Math.Abs(portfolioVega) < portfolioValue * 0.20); // Vega < 20% of portfolio
            
            // ============================================
            // STEP 8: Simulate Earnings Scenario
            // ============================================
            Console.WriteLine("\n=== Earnings Simulation ===");
            
            // Scenario 1: Expected IV crush (most likely)
            var postEarningsIV = signal.IV30 * 0.7; // 30% IV crush
            var scenario1PnL = SimulateEarningsOutcome(
                calendarPricing,
                underlyingMove: 0.02, // 2% move
                ivChange: -0.30, // 30% IV drop
                positionSize.Contracts
            );
            
            Console.WriteLine($"Scenario 1 (IV Crush): ${scenario1PnL:F2}");
            
            // Scenario 2: Large move (adverse)
            var scenario2PnL = SimulateEarningsOutcome(
                calendarPricing,
                underlyingMove: 0.08, // 8% move
                ivChange: -0.20, // 20% IV drop
                positionSize.Contracts
            );
            
            Console.WriteLine($"Scenario 2 (Large Move): ${scenario2PnL:F2}");
            
            // Scenario 3: No move, no IV change (worst case)
            var scenario3PnL = SimulateEarningsOutcome(
                calendarPricing,
                underlyingMove: 0.0,
                ivChange: 0.0,
                positionSize.Contracts
            );
            
            Console.WriteLine($"Scenario 3 (No Change): ${scenario3PnL:F2}");
            
            // Expected value calculation
            var expectedPnL = 0.60 * scenario1PnL + // 60% probability of normal IV crush
                             0.30 * scenario2PnL + // 30% probability of large move
                             0.10 * scenario3PnL;  // 10% probability of no change
            
            Console.WriteLine($"\nExpected P&L: ${expectedPnL:F2}");
            Console.WriteLine($"Expected Return on Risk: {expectedPnL / positionSize.DollarAllocation:P2}");
            
            Assert.True(expectedPnL > 0); // Positive expected value
        }
        
        // ============================================
        // Helper Methods for Test Data Generation
        // ============================================
        
        private List<YangZhangEstimator.PriceBar> GeneratePriceHistory(
            double basePrice, double volatility, int days)
        {
            var bars = new List<YangZhangEstimator.PriceBar>();
            var random = new Random(42); // Seed for reproducibility
            var currentPrice = basePrice;
            
            for (int i = 0; i < days; i++)
            {
                var dailyReturn = random.NextGaussian(0, volatility / Math.Sqrt(252));
                currentPrice *= (1 + dailyReturn);
                
                var open = currentPrice * (1 + random.NextGaussian(0, volatility / Math.Sqrt(252) / 4));
                var high = Math.Max(open, currentPrice) * (1 + Math.Abs(random.NextGaussian(0, volatility / Math.Sqrt(252) / 4)));
                var low = Math.Min(open, currentPrice) * (1 - Math.Abs(random.NextGaussian(0, volatility / Math.Sqrt(252) / 4)));
                var close = currentPrice;
                
                bars.Add(new YangZhangEstimator.PriceBar
                {
                    Date = DateTime.Today.AddDays(-days + i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = 10_000_000 + random.Next(-2_000_000, 2_000_000)
                });
            }
            
            return bars;
        }
        
        private OptionChain GenerateOptionChain(
            double underlyingPrice, DateTime earningsDate, DateTime currentDate)
        {
            var chain = new OptionChain { Expiries = new List<OptionExpiry>() };
            
            // Generate multiple expiries with earnings vol premium
            var expiryDates = new[]
            {
                earningsDate.AddDays(1),  // Front month (day after earnings)
                earningsDate.AddDays(8),  // Weekly after earnings
                earningsDate.AddDays(22), // Monthly
                earningsDate.AddDays(50)  // Back month
            };
            
            foreach (var expiryDate in expiryDates)
            {
                var daysToExpiry = (expiryDate - currentDate).Days;
                var isEarningsExpiry = Math.Abs((expiryDate - earningsDate).Days) <= 3;
                
                // Earnings expiries have elevated IV
                var baseIV = isEarningsExpiry ? 0.45 : 0.25;
                
                // Add term structure slope
                baseIV += daysToExpiry * -0.0005; // Backwardation
                
                var expiry = new OptionExpiry
                {
                    ExpiryDate = expiryDate,
                    Calls = new List<OptionContract>(),
                    Puts = new List<OptionContract>()
                };
                
                // Generate strikes around ATM
                for (double strike = underlyingPrice * 0.9; strike <= underlyingPrice * 1.1; strike += 2.5)
                {
                    var moneyness = strike / underlyingPrice;
                    var skewAdjustment = Math.Pow(moneyness - 1, 2) * 0.5; // Volatility smile
                    
                    var callIV = baseIV + skewAdjustment;
                    var putIV = baseIV + skewAdjustment;
                    
                    // Use Black-Scholes for option prices (simplified)
                    var callPrice = BlackScholesCall(underlyingPrice, strike, 0.045, callIV, daysToExpiry / 365.0);
                    var putPrice = BlackScholesPut(underlyingPrice, strike, 0.045, putIV, daysToExpiry / 365.0);
                    
                    expiry.Calls.Add(new OptionContract
                    {
                        Strike = strike,
                        Bid = callPrice * 0.98,
                        Ask = callPrice * 1.02,
                        ImpliedVolatility = callIV,
                        Volume = 1000,
                        OpenInterest = 5000
                    });
                    
                    expiry.Puts.Add(new OptionContract
                    {
                        Strike = strike,
                        Bid = putPrice * 0.98,
                        Ask = putPrice * 1.02,
                        ImpliedVolatility = putIV,
                        Volume = 800,
                        OpenInterest = 4000
                    });
                }
                
                chain.Expiries.Add(expiry);
            }
            
            return chain;
        }
        
        private List<TermStructure.TermStructurePoint> ExtractTermStructurePoints(
            OptionChain chain, double underlyingPrice)
        {
            var points = new List<TermStructure.TermStructurePoint>();
            
            foreach (var expiry in chain.Expiries)
            {
                var atmCall = expiry.Calls
                    .OrderBy(c => Math.Abs(c.Strike - underlyingPrice))
                    .First();
                var atmPut = expiry.Puts
                    .OrderBy(p => Math.Abs(p.Strike - underlyingPrice))
                    .First();
                
                var atmIV = (atmCall.ImpliedVolatility + atmPut.ImpliedVolatility) / 2;
                
                points.Add(new TermStructure.TermStructurePoint
                {
                    DaysToExpiry = (expiry.ExpiryDate - DateTime.Today).Days,
                    ImpliedVolatility = atmIV,
                    Strike = atmCall.Strike,
                    ExpiryDate = expiry.ExpiryDate
                });
            }
            
            return points;
        }
        
        private List<TradeResult> GenerateHistoricalTrades()
        {
            var trades = new List<TradeResult>();
            var random = new Random(42);
            
            // Generate 50 historical trades with realistic win rate
            for (int i = 0; i < 50; i++)
            {
                var isWin = random.NextDouble() < 0.68; // 68% win rate
                
                trades.Add(new TradeResult
                {
                    Symbol = $"TEST{i}",
                    EntryDate = DateTime.Today.AddDays(-100 + i * 2),
                    ExitDate = DateTime.Today.AddDays(-95 + i * 2),
                    PnL = isWin ? 
                        random.NextDouble() * 500 + 100 :  // Win: $100-$600
                        -(random.NextDouble() * 300 + 50), // Loss: $50-$350
                    ReturnPercent = isWin ?
                        random.NextDouble() * 0.25 + 0.05 :  // Win: 5%-30%
                        -(random.NextDouble() * 0.15 + 0.05) // Loss: 5%-20%
                });
            }
            
            return trades;
        }
        
        private double SimulateEarningsOutcome(
            CalendarSpreadPricing pricing,
            double underlyingMove,
            double ivChange,
            int contracts)
        {
            // Simplified P&L calculation
            var newUnderlying = pricing.FrontOption.Price * (1 + underlyingMove);
            
            // Front option expires (we were short)
            var frontValue = Math.Max(0, newUnderlying - pricing.FrontOption.Price);
            
            // Back option value change (simplified using vega)
            var backValue = pricing.BackOption.Price + 
                          (pricing.BackOption.Vega * ivChange * 100) +
                          (pricing.BackOption.Delta * underlyingMove * pricing.BackOption.Price);
            
            var spreadPnL = (backValue - frontValue - pricing.SpreadCost) * contracts * 100;
            
            return spreadPnL;
        }
        
        // Simplified Black-Scholes formulas for testing
        private double BlackScholesCall(double S, double K, double r, double sigma, double T)
        {
            var d1 = (Math.Log(S / K) + (r + sigma * sigma / 2) * T) / (sigma * Math.Sqrt(T));
            var d2 = d1 - sigma * Math.Sqrt(T);
            return S * NormalCDF(d1) - K * Math.Exp(-r * T) * NormalCDF(d2);
        }
        
        private double BlackScholesPut(double S, double K, double r, double sigma, double T)
        {
            var d1 = (Math.Log(S / K) + (r + sigma * sigma / 2) * T) / (sigma * Math.Sqrt(T));
            var d2 = d1 - sigma * Math.Sqrt(T);
            return K * Math.Exp(-r * T) * NormalCDF(-d2) - S * NormalCDF(-d1);
        }
        
        private double NormalCDF(double x)
        {
            return 0.5 * (1 + Erf(x / Math.Sqrt(2)));
        }
        
        private double Erf(double x)
        {
            // Approximation of error function
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
    
    // Mock implementations for testing
    public class MockMarketDataProvider : IMarketDataProvider
    {
        private readonly List<YangZhangEstimator.PriceBar> _priceHistory;
        private readonly OptionChain _optionChain;
        private readonly double _currentPrice;
        
        public MockMarketDataProvider(
            List<YangZhangEstimator.PriceBar> priceHistory,
            OptionChain optionChain,
            double currentPrice)
        {
            _priceHistory = priceHistory;
            _optionChain = optionChain;
            _currentPrice = currentPrice;
        }
        
        public Task<List<YangZhangEstimator.PriceBar>> GetPriceHistory(string symbol, int days)
        {
            return Task.FromResult(_priceHistory.TakeLast(days).ToList());
        }
        
        public Task<OptionChain> GetOptionChain(string symbol)
        {
            return Task.FromResult(_optionChain);
        }
        
        public Task<double> GetCurrentPrice(string symbol)
        {
            return Task.FromResult(_currentPrice);
        }
    }
    
    public class MockPricingEngine : IOptionPricingEngine
    {
        public Task<OptionPricing> PriceOption(OptionParameters parameters)
        {
            // Simplified pricing for testing
            return Task.FromResult(new OptionPricing
            {
                Price = 5.0,
                Delta = 0.5,
                Gamma = 0.02,
                Vega = 0.15,
                Theta = -0.05,
                Rho = 0.03
            });
        }
    }
    
    // Extension for random number generation
    public static class RandomExtensions
    {
        public static double NextGaussian(this Random random, double mean, double stdDev)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }
    }
}