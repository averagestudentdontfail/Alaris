using Xunit;
using FluentAssertions;
using Alaris.Strategy;
using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;
using Alaris.Strategy.Risk;

namespace Alaris.Test.Integration;

/// <summary>
/// Integration tests for the earnings volatility calendar spread strategy.
/// Tests updated to use realistic data that meets trading criteria thresholds.
/// </summary>
public class StrategyIntegrationTests
{
    [Fact]
    public void YangZhangEstimator_CalculatesVolatilityCorrectly()
    {
        // Arrange
        var estimator = new YangZhangEstimator();
        var priceBars = GenerateSamplePriceBars(50);

        // Act
        var volatility = estimator.Calculate(priceBars, 30, annualized: true);

        // Assert
        volatility.Should().BeGreaterThan(0);
        volatility.Should().BeLessThan(2.0);
    }

    [Fact]
    public void YangZhangEstimator_CalculatesRollingVolatility()
    {
        // Arrange
        var estimator = new YangZhangEstimator();
        var priceBars = GenerateSamplePriceBars(100);

        // Act
        var rollingVol = estimator.CalculateRolling(priceBars, 30, annualized: true);

        // Assert
        rollingVol.Should().NotBeEmpty();
        rollingVol.Should().HaveCount(100 - 30);
        rollingVol.All(v => v.Volatility > 0).Should().BeTrue();
    }

    [Fact]
    public void TermStructureAnalyzer_IdentifiesInvertedStructure()
    {
        // Arrange
        var analyzer = new TermStructureAnalyzer();
        
        // CORRECTED: Use steeper inversion to meet -0.00406 threshold
        // Trading criterion requires slope <= -0.00406 (from Atilgan 2014)
        // Over 35 days (45-10), need drop >= 35 * 0.00406 = 0.142 (14.2%)
        var points = new List<TermStructurePoint>
        {
            new() { DaysToExpiry = 10, ImpliedVolatility = 0.45, Strike = 100 },
            new() { DaysToExpiry = 20, ImpliedVolatility = 0.37, Strike = 100 },
            new() { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
            new() { DaysToExpiry = 45, ImpliedVolatility = 0.23, Strike = 100 }
        };

        // Act
        var analysis = analyzer.Analyze(points);

        // Assert
        analysis.IsInverted.Should().BeTrue();
        analysis.Slope.Should().BeLessThan(0);
        analysis.MeetsTradingCriterion.Should().BeTrue();
    }

    [Fact]
    public void TermStructureAnalyzer_IdentifiesNormalStructure()
    {
        // Arrange
        var analyzer = new TermStructureAnalyzer();
        var points = new List<TermStructurePoint>
        {
            new() { DaysToExpiry = 10, ImpliedVolatility = 0.25, Strike = 100 },
            new() { DaysToExpiry = 20, ImpliedVolatility = 0.28, Strike = 100 },
            new() { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
            new() { DaysToExpiry = 45, ImpliedVolatility = 0.32, Strike = 100 }
        };

        // Act
        var analysis = analyzer.Analyze(points);

        // Assert
        analysis.IsInverted.Should().BeFalse();
        analysis.Slope.Should().BeGreaterThan(0);
        analysis.MeetsTradingCriterion.Should().BeFalse();
    }

    [Fact]
    public void SignalGenerator_GeneratesRecommendedSignalWhenAllCriteriaMet()
    {
        // Arrange
        var generator = new SignalGenerator();
        var dataProvider = new MockMarketDataProvider();
        var pricingEngine = new MockPricingEngine();

        // CORRECTED: Create analysis with steep enough slope to meet threshold
        var analysis = new TermStructureAnalysis
        {
            Slope = -0.0050,  // Steeper than -0.00406 threshold
            Intercept = 0.45,
            RSquared = 0.95,
            IsInverted = true,
            Points = new List<TermStructurePoint>
            {
                new() { DaysToExpiry = 10, ImpliedVolatility = 0.45, Strike = 100 },
                new() { DaysToExpiry = 20, ImpliedVolatility = 0.37, Strike = 100 },
                new() { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
                new() { DaysToExpiry = 45, ImpliedVolatility = 0.23, Strike = 100 }
            }
        };

        var context = new TradingContext
        {
            Symbol = "AAPL",
            SpotPrice = 150.0,
            TermStructure = analysis,
            IV30 = 0.35,
            RV30 = 0.25,  // IV30/RV30 = 1.4 > 1.0 (elevated IV)
            DaysToEarnings = 7,
            DataProvider = dataProvider,
            PricingEngine = pricingEngine
        };

        // Act
        var signal = generator.Generate(context);

        // Assert
        signal.Strength.Should().Be(SignalStrength.Recommended);
        signal.Rationale.Should().Contain("inverted");
    }

    [Fact]
    public void SignalGenerator_GeneratesAvoidSignalWhenCriteriaNotMet()
    {
        // Arrange
        var generator = new SignalGenerator();
        var dataProvider = new MockMarketDataProvider();
        var pricingEngine = new MockPricingEngine();

        // Normal (not inverted) term structure
        var analysis = new TermStructureAnalysis
        {
            Slope = 0.002,  // Positive slope
            Intercept = 0.25,
            RSquared = 0.90,
            IsInverted = false,
            Points = new List<TermStructurePoint>()
        };

        var context = new TradingContext
        {
            Symbol = "AAPL",
            SpotPrice = 150.0,
            TermStructure = analysis,
            IV30 = 0.25,
            RV30 = 0.30,  // IV < RV (not elevated)
            DaysToEarnings = 7,
            DataProvider = dataProvider,
            PricingEngine = pricingEngine
        };

        // Act
        var signal = generator.Generate(context);

        // Assert
        signal.Strength.Should().Be(SignalStrength.Avoid);
    }

    [Fact]
    public void KellyPositionSizer_CalculatesReasonablePosition()
    {
        // Arrange
        var sizer = new KellyPositionSizer();
        
        // CORRECTED: Use parameters that lead to positive position size
        var opportunity = new TradingOpportunity
        {
            Signal = new TradingSignal
            {
                Strength = SignalStrength.Recommended,  // Must be Recommended
                Confidence = 0.75,
                Direction = TradeDirection.Long
            },
            SpreadPricing = new CalendarSpreadPricing  // Must not be null
            {
                FrontOption = new OptionPricing { Price = 3.00, Delta = 0.50 },
                BackOption = new OptionPricing { Price = 5.50, Delta = 0.45 },
                SpreadCost = 2.50,
                MaxProfit = 5.00,
                MaxLoss = 2.50,
                HasPositiveExpectedValue = true
            },
            ExpectedReturn = 0.25,  // 25% expected return
            Risk = 0.10  // 10% risk per unit
        };

        var account = new AccountInfo
        {
            TotalCapital = 100000.0,
            RiskCapital = 10000.0,
            MaxPositionSize = 50
        };

        // Act
        var position = sizer.Calculate(opportunity, account);

        // Assert
        position.Contracts.Should().BeGreaterThan(0);
        position.Contracts.Should().BeLessOrEqualTo(account.MaxPositionSize);
        position.CapitalAllocated.Should().BeGreaterThan(0);
        position.CapitalAllocated.Should().BeLessOrEqualTo(account.RiskCapital);
    }

    [Fact]
    public void KellyPositionSizer_ReturnsZeroForAvoidSignal()
    {
        // Arrange
        var sizer = new KellyPositionSizer();
        
        var opportunity = new TradingOpportunity
        {
            Signal = new TradingSignal
            {
                Strength = SignalStrength.Avoid,  // Avoid signal
                Confidence = 0.50,
                Direction = TradeDirection.Neutral
            },
            SpreadPricing = null,  // No pricing for avoided signals
            ExpectedReturn = 0.0,
            Risk = 0.0
        };

        var account = new AccountInfo
        {
            TotalCapital = 100000.0,
            RiskCapital = 10000.0,
            MaxPositionSize = 50
        };

        // Act
        var position = sizer.Calculate(opportunity, account);

        // Assert
        position.Contracts.Should().Be(0);
        position.CapitalAllocated.Should().Be(0);
    }

    [Fact]
    public void Control_EvaluatesCompleteOpportunity()
    {
        // Arrange
        var control = new StrategyControl();
        var dataProvider = new MockMarketDataProvider();
        var pricingEngine = new MockPricingEngine();

        // CORRECTED: Use strong signal that will generate SpreadPricing
        var context = new TradingContext
        {
            Symbol = "AAPL",
            SpotPrice = 150.0,
            TermStructure = new TermStructureAnalysis
            {
                Slope = -0.0055,  // Strong inversion
                Intercept = 0.48,
                RSquared = 0.96,
                IsInverted = true,
                Points = new List<TermStructurePoint>
                {
                    new() { DaysToExpiry = 10, ImpliedVolatility = 0.48, Strike = 100 },
                    new() { DaysToExpiry = 20, ImpliedVolatility = 0.38, Strike = 100 },
                    new() { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
                    new() { DaysToExpiry = 45, ImpliedVolatility = 0.20, Strike = 100 }
                }
            },
            IV30 = 0.40,
            RV30 = 0.25,  // Strong IV elevation
            DaysToEarnings = 7,
            DataProvider = dataProvider,
            PricingEngine = pricingEngine
        };

        // Act
        var opportunity = control.Evaluate(context);

        // Assert
        opportunity.Should().NotBeNull();
        opportunity.Signal.Should().NotBeNull();
        opportunity.Signal.Strength.Should().Be(SignalStrength.Recommended);
        opportunity.SpreadPricing.Should().NotBeNull();  // Now this should be populated
        opportunity.ExpectedReturn.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Control_EvaluatesAvoidOpportunity()
    {
        // Arrange
        var control = new StrategyControl();
        var dataProvider = new MockMarketDataProvider();
        var pricingEngine = new MockPricingEngine();

        // Weak signal (normal term structure)
        var context = new TradingContext
        {
            Symbol = "AAPL",
            SpotPrice = 150.0,
            TermStructure = new TermStructureAnalysis
            {
                Slope = 0.002,  // Normal structure
                Intercept = 0.25,
                RSquared = 0.90,
                IsInverted = false,
                Points = new List<TermStructurePoint>()
            },
            IV30 = 0.25,
            RV30 = 0.28,  // No IV elevation
            DaysToEarnings = 7,
            DataProvider = dataProvider,
            PricingEngine = pricingEngine
        };

        // Act
        var opportunity = control.Evaluate(context);

        // Assert
        opportunity.Should().NotBeNull();
        opportunity.Signal.Strength.Should().Be(SignalStrength.Avoid);
        opportunity.SpreadPricing.Should().BeNull();  // No pricing for avoided opportunities
    }

    // Helper methods

    private List<PriceBar> GenerateSamplePriceBars(int count)
    {
        var bars = new List<PriceBar>();
        var basePrice = 100.0;
        var random = new Random(42);  // Fixed seed for reproducibility

        for (int i = 0; i < count; i++)
        {
            var open = basePrice + random.NextDouble() * 2 - 1;
            var close = open + random.NextDouble() * 2 - 1;
            var high = Math.Max(open, close) + random.NextDouble();
            var low = Math.Min(open, close) - random.NextDouble();

            bars.Add(new PriceBar
            {
                Date = DateTime.Today.AddDays(-count + i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = random.Next(1000000, 10000000)
            });

            basePrice = close;
        }

        return bars;
    }
}

// Mock implementations for testing

internal class MockMarketDataProvider : IMarketDataProvider
{
    public Task<List<PriceBar>> GetHistoricalPrices(string symbol, DateTime start, DateTime end)
    {
        var bars = new List<PriceBar>();
        var random = new Random(42);
        var days = (end - start).Days;

        for (int i = 0; i < days; i++)
        {
            bars.Add(new PriceBar
            {
                Date = start.AddDays(i),
                Open = 100.0 + random.NextDouble() * 10,
                High = 105.0 + random.NextDouble() * 10,
                Low = 95.0 + random.NextDouble() * 10,
                Close = 100.0 + random.NextDouble() * 10,
                Volume = random.Next(1000000, 10000000)
            });
        }

        return Task.FromResult(bars);
    }

    public Task<OptionChain> GetOptionChain(string symbol, DateTime expiry)
    {
        return Task.FromResult(new OptionChain
        {
            Symbol = symbol,
            Expiry = expiry,
            Calls = new List<OptionQuote>(),
            Puts = new List<OptionQuote>()
        });
    }

    public Task<List<DateTime>> GetEarningsDates(string symbol)
    {
        return Task.FromResult(new List<DateTime> 
        { 
            DateTime.Today.AddDays(7),
            DateTime.Today.AddDays(97) 
        });
    }

    public Task<bool> IsDataAvailable(string symbol)
    {
        return Task.FromResult(true);
    }
}

internal class MockPricingEngine : IOptionPricingEngine
{
    public Task<OptionPricing> PriceOption(OptionParameters parameters)
    {
        return Task.FromResult(new OptionPricing
        {
            Price = 3.00,
            Delta = 0.50,
            Gamma = 0.05,
            Vega = 0.15,
            Theta = -0.03,
            Rho = 0.02,
            ImpliedVolatility = parameters.ImpliedVolatility,
            TimeToExpiry = parameters.TimeToExpiry
        });
    }

    public Task<CalendarSpreadPricing> PriceCalendarSpread(CalendarSpreadParameters parameters)
    {
        return Task.FromResult(new CalendarSpreadPricing
        {
            FrontOption = new OptionPricing { Price = 3.00, Delta = 0.50 },
            BackOption = new OptionPricing { Price = 5.50, Delta = 0.45 },
            SpreadCost = 2.50,
            SpreadDelta = -0.05,
            SpreadVega = 0.10,
            SpreadTheta = 0.03,
            MaxProfit = 5.00,
            MaxLoss = 2.50,
            HasPositiveExpectedValue = true
        });
    }

    public Task<double> CalculateImpliedVolatility(double marketPrice, OptionParameters parameters)
    {
        return Task.FromResult(0.30);
    }
}