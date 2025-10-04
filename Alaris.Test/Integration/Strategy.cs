using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Alaris.Double;
using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Risk;

namespace Alaris.Test.Integration
{
    /// <summary>
    /// End-to-end tests validating double boundary engine with Alaris strategy components.
    /// </summary>
    public class Strategy
    {
        [Fact]
        public async Task CompleteWorkflow_WithNegativeRates_ProducesValidRecommendation()
        {
            // Arrange
            var testSymbol = "AAPL";
            var currentDate = new Date(15, Month.March, 2024);
            var earningsDate = new DateTime(2024, 4, 25);
            
            Settings.instance().setEvaluationDate(currentDate);
            
            // Create market data with NEGATIVE rates
            var priceHistory = GeneratePriceHistory(basePrice: 150.0, volatility: 0.25, days: 90);
            var optionChain = GenerateOptionChain(
                underlyingPrice: 150.0,
                earningsDate: earningsDate,
                currentDate: DateTime.Now);
            
            var marketData = new MockMarketDataProvider(priceHistory, optionChain, 150.0);
            var yangZhang = new YangZhangEstimator();
            var termStructure = new TermStructure();
            
            // Act 1: Calculate realized volatility
            var rv30 = yangZhang.Calculate(priceHistory, 30);
            
            // Act 2: Analyze term structure
            var termPoints = ExtractTermStructurePoints(optionChain, 150.0);
            var termAnalysis = termStructure.Analyze(termPoints);
            
            // Act 3: Price calendar spread with double boundary engine
            var spreadParams = new CalendarSpreadParameters
            {
                UnderlyingPrice = 150.0,
                Strike = 150.0,
                FrontExpiry = new Date(26, Month.April, 2024),
                BackExpiry = new Date(17, Month.May, 2024),
                ImpliedVolatility = termAnalysis.Intercept,
                RiskFreeRate = -0.005,  // NEGATIVE!
                DividendYield = -0.01,  // NEGATIVE!
                OptionType = Option.Type.Call,
                ValuationDate = currentDate
            };
            
            var calendarPricing = await PriceCalendarSpreadWithDouble(spreadParams);
            
            // Assert
            calendarPricing.Should().NotBeNull();
            calendarPricing.SpreadCost.Should().BeGreaterThan(0, "calendar spread should have positive cost");
            
            // Delta should be near neutral for calendar spread
            Math.Abs(calendarPricing.SpreadDelta).Should().BeLessThan(0.15,
                "calendar spread should be approximately delta neutral");
            
            // Vega should be positive (benefit from IV increase)
            calendarPricing.SpreadVega.Should().BeGreaterThan(0,
                "calendar spread should have positive vega exposure");
            
            Console.WriteLine("\n=== Calendar Spread with Negative Rates ===");
            Console.WriteLine($"Front Option: ${calendarPricing.FrontOption.Price:F4}");
            Console.WriteLine($"Back Option: ${calendarPricing.BackOption.Price:F4}");
            Console.WriteLine($"Spread Cost: ${calendarPricing.SpreadCost:F4}");
            Console.WriteLine($"Spread Delta: {calendarPricing.SpreadDelta:F4}");
            Console.WriteLine($"Spread Vega: {calendarPricing.SpreadVega:F4}");
        }
        
        [Fact]
        public async Task CalendarSpread_NegativeVsPositiveRates_ProduceDifferentPrices()
        {
            // Arrange
            var currentDate = new Date(15, Month.March, 2024);
            Settings.instance().setEvaluationDate(currentDate);
            
            var baseParams = new CalendarSpreadParameters
            {
                UnderlyingPrice = 100.0,
                Strike = 100.0,
                FrontExpiry = new Date(15, Month.April, 2024),
                BackExpiry = new Date(15, Month.May, 2024),
                ImpliedVolatility = 0.25,
                DividendYield = 0.02,
                OptionType = Option.Type.Put,
                ValuationDate = currentDate
            };
            
            // Act 1: Price with positive rates
            var positiveParams = baseParams with { RiskFreeRate = 0.05 };
            var positivePricing = await PriceCalendarSpreadWithDouble(positiveParams);
            
            // Act 2: Price with negative rates (double boundary regime)
            var negativeParams = baseParams with 
            { 
                RiskFreeRate = -0.005,
                DividendYield = -0.01 
            };
            var negativePricing = await PriceCalendarSpreadWithDouble(negativeParams);
            
            // Assert - Prices should differ
            positivePricing.SpreadCost.Should().NotBeApproximately(
                negativePricing.SpreadCost, 0.01,
                "positive and negative rate regimes should produce different prices");
            
            Console.WriteLine($"Positive rates spread: ${positivePricing.SpreadCost:F4}");
            Console.WriteLine($"Negative rates spread: ${negativePricing.SpreadCost:F4}");
            Console.WriteLine($"Difference: ${Math.Abs(positivePricing.SpreadCost - negativePricing.SpreadCost):F4}");
        }
        
        [Fact]
        public void PositionSizing_WithNegativeRatePricing_ProducesValidAllocations()
        {
            // Arrange
            var portfolioValue = 100000.0;
            var spreadCost = 2.50; // From negative rate pricing
            
            var signal = new Signal
            {
                Symbol = "TEST",
                Strength = SignalStrength.Recommended,
                IVRVRatio = 1.35,
                TermStructureSlope = -0.005,
                AverageVolume = 2_000_000,
                Criteria = new Dictionary<string, bool>
                {
                    ["Volume"] = true,
                    ["IV/RV"] = true,
                    ["TermSlope"] = true
                }
            };
            
            var historicalTrades = GenerateHistoricalTrades(winRate: 0.68);
            var positionSizer = new KellyPositionSizer();
            
            // Act
            var positionSize = positionSizer.CalculateFromHistory(
                portfolioValue,
                historicalTrades,
                spreadCost,
                signal);
            
            // Assert
            positionSize.Contracts.Should().BeGreaterThan(0);
            positionSize.AllocationPercent.Should().BeInRange(0.01, 0.06);
            positionSize.DollarAllocation.Should().BeLessThanOrEqualTo(portfolioValue * 0.06);
            
            Console.WriteLine($"Position size: {positionSize.Contracts} contracts");
            Console.WriteLine($"Allocation: {positionSize.AllocationPercent:P2} (${positionSize.DollarAllocation:F2})");
        }
        
        private async Task<CalendarSpreadPricing> PriceCalendarSpreadWithDouble(
            CalendarSpreadParameters parameters)
        {
            // Create process
            var spot = new SimpleQuote(parameters.UnderlyingPrice);
            var spotHandle = new QuoteHandle(spot);
            
            var riskFreeRate = new FlatForward(
                parameters.ValuationDate,
                parameters.RiskFreeRate,
                new Actual365Fixed());
            
            var dividendYield = new FlatForward(
                parameters.ValuationDate,
                parameters.DividendYield,
                new Actual365Fixed());
            
            var volatility = new BlackConstantVol(
                parameters.ValuationDate,
                new TARGET(),
                parameters.ImpliedVolatility,
                new Actual365Fixed());
            
            var process = new GeneralizedBlackScholesProcess(
                spotHandle,
                new YieldTermStructureHandle(dividendYield),
                new YieldTermStructureHandle(riskFreeRate),
                new BlackVolTermStructureHandle(volatility));
            
            // Use double boundary engine
            var engine = new DoubleBoundaryEngine(
                process,
                QdFpAmericanEngine.highPrecisionScheme());
            
            // Price both legs
            var frontOption = await PriceOption(engine, parameters, parameters.FrontExpiry);
            var backOption = await PriceOption(engine, parameters, parameters.BackExpiry);
            
            return new CalendarSpreadPricing
            {
                FrontOption = frontOption,
                BackOption = backOption,
                SpreadCost = backOption.Price - frontOption.Price,
                SpreadDelta = backOption.Delta - frontOption.Delta,
                SpreadGamma = backOption.Gamma - frontOption.Gamma,
                SpreadVega = backOption.Vega - frontOption.Vega,
                SpreadTheta = backOption.Theta - frontOption.Theta,
                MaxProfit = backOption.Price * 0.25,
                MaxLoss = backOption.Price - frontOption.Price,
                BreakEven = parameters.Strike
            };
        }
        
        private async Task<OptionPricing> PriceOption(
            DoubleBoundaryEngine engine,
            CalendarSpreadParameters parameters,
            Date expiry)
        {
            var exercise = new AmericanExercise(parameters.ValuationDate, expiry);
            var payoff = new PlainVanillaPayoff(parameters.OptionType, parameters.Strike);
            var option = new VanillaOption(payoff, exercise);
            
            option.setPricingEngine(engine);
            
            return await Task.FromResult(new OptionPricing
            {
                Price = option.NPV(),
                Delta = option.delta(),
                Gamma = option.gamma(),
                Vega = option.vega(),
                Theta = option.theta(),
                Rho = option.rho()
            });
        }
        
        // Helper methods for test data generation
        private List<YangZhangEstimator.PriceBar> GeneratePriceHistory(
            double basePrice, double volatility, int days)
        {
            var bars = new List<YangZhangEstimator.PriceBar>();
            var random = new Random(42);
            var currentPrice = basePrice;
            
            for (int i = 0; i < days; i++)
            {
                var dailyReturn = random.NextGaussian(0, volatility / Math.Sqrt(252));
                currentPrice *= (1 + dailyReturn);
                
                var open = currentPrice * (1 + random.NextGaussian(0, volatility / Math.Sqrt(252) / 4));
                var high = Math.Max(open, currentPrice) * (1 + Math.Abs(random.NextGaussian(0, volatility / Math.Sqrt(252) / 4)));
                var low = Math.Min(open, currentPrice) * (1 - Math.Abs(random.NextGaussian(0, volatility / Math.Sqrt(252) / 4)));
                
                bars.Add(new YangZhangEstimator.PriceBar
                {
                    Date = DateTime.Today.AddDays(-days + i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = currentPrice,
                    Volume = 10_000_000
                });
            }
            
            return bars;
        }
        
        private OptionChain GenerateOptionChain(
            double underlyingPrice, DateTime earningsDate, DateTime currentDate)
        {
            var chain = new OptionChain { Expiries = new List<OptionExpiry>() };
            
            var expiryDates = new[]
            {
                earningsDate.AddDays(1),
                earningsDate.AddDays(22),
            };
            
            foreach (var expiryDate in expiryDates)
            {
                var expiry = new OptionExpiry
                {
                    ExpiryDate = expiryDate,
                    Calls = new List<OptionContract>(),
                    Puts = new List<OptionContract>()
                };
                
                for (double strike = underlyingPrice * 0.9; strike <= underlyingPrice * 1.1; strike += 5)
                {
                    expiry.Calls.Add(new OptionContract
                    {
                        Strike = strike,
                        Bid = 5.0,
                        Ask = 5.2,
                        ImpliedVolatility = 0.25
                    });
                    
                    expiry.Puts.Add(new OptionContract
                    {
                        Strike = strike,
                        Bid = 4.8,
                        Ask = 5.0,
                        ImpliedVolatility = 0.25
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
                var atmCall = expiry.Calls.OrderBy(c => Math.Abs(c.Strike - underlyingPrice)).First();
                var atmPut = expiry.Puts.OrderBy(p => Math.Abs(p.Strike - underlyingPrice)).First();
                
                points.Add(new TermStructure.TermStructurePoint
                {
                    DaysToExpiry = (expiry.ExpiryDate - DateTime.Today).Days,
                    ImpliedVolatility = (atmCall.ImpliedVolatility + atmPut.ImpliedVolatility) / 2,
                    Strike = atmCall.Strike,
                    ExpiryDate = expiry.ExpiryDate
                });
            }
            
            return points;
        }
        
        private List<TradeResult> GenerateHistoricalTrades(double winRate = 0.68, int count = 50)
        {
            var trades = new List<TradeResult>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                var isWin = random.NextDouble() < winRate;
                
                trades.Add(new TradeResult
                {
                    Symbol = $"TEST{i}",
                    EntryDate = DateTime.Today.AddDays(-100 + i * 2),
                    ExitDate = DateTime.Today.AddDays(-95 + i * 2),
                    PnL = isWin ? random.NextDouble() * 500 + 100 : -(random.NextDouble() * 300 + 50),
                    ReturnPercent = isWin ? random.NextDouble() * 0.25 + 0.05 : -(random.NextDouble() * 0.15 + 0.05)
                });
            }
            
            return trades;
        }
    }
    
    // Mock provider for testing
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
            => Task.FromResult(_priceHistory.TakeLast(days).ToList());
        
        public Task<OptionChain> GetOptionChain(string symbol)
            => Task.FromResult(_optionChain);
        
        public Task<double> GetCurrentPrice(string symbol)
            => Task.FromResult(_currentPrice);
    }
    
    // Extension for Gaussian random numbers
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
