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
        var points = new List<TermStructurePoint>
        {
            new() { DaysToExpiry = 10, ImpliedVolatility = 0.40, Strike = 100 },
            new() { DaysToExpiry = 20, ImpliedVolatility = 0.35, Strike = 100 },
            new() { DaysToExpiry = 30, ImpliedVolatility = 0.32, Strike = 100 },
            new() { DaysToExpiry = 45, ImpliedVolatility = 0.28, Strike = 100 }
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
    public void SignalGenerator_GeneratesAvoidSignalWhenCriteriaNotMet()
    {
        // Arrange
        var mockData = new MockMarketDataProvider(
            averageVolume: 500_000, // Below threshold
            impliedVol: 0.25,
            realizedVol: 0.30); // IV/RV < 1.25
        
        var yangZhang = new YangZhangEstimator();
        var termAnalyzer = new TermStructureAnalyzer();
        var generator = new SignalGenerator(mockData, yangZhang, termAnalyzer);

        // Act
        var signal = generator.Generate("TEST", DateTime.Today.AddDays(7), DateTime.Today);

        // Assert
        signal.Strength.Should().Be(SignalStrength.Avoid);
        signal.Criteria["Volume"].Should().BeFalse();
    }

    [Fact]
    public void SignalGenerator_GeneratesRecommendedSignalWhenAllCriteriaMet()
    {
        // Arrange
        var mockData = new MockMarketDataProvider(
            averageVolume: 2_000_000, // Above threshold
            impliedVol: 0.35,
            realizedVol: 0.25); // IV/RV > 1.25
        
        var yangZhang = new YangZhangEstimator();
        var termAnalyzer = new TermStructureAnalyzer();
        var generator = new SignalGenerator(mockData, yangZhang, termAnalyzer);

        // Act
        var signal = generator.Generate("TEST", DateTime.Today.AddDays(7), DateTime.Today);

        // Assert
        signal.Strength.Should().Be(SignalStrength.Recommended);
        signal.Criteria["Volume"].Should().BeTrue();
        signal.Criteria["IV/RV"].Should().BeTrue();
        signal.Criteria["TermSlope"].Should().BeTrue();
    }

    [Fact]
    public void CalendarSpreadPricing_ValidatesCorrectly()
    {
        // Arrange
        var pricing = new CalendarSpreadPricing
        {
            FrontOption = new OptionPricing { Price = 3.50 },
            BackOption = new OptionPricing { Price = 6.00 },
            SpreadCost = 2.50,
            MaxProfit = 4.00,
            MaxLoss = 2.50
        };

        // Act & Assert
        pricing.Invoking(p => p.Validate()).Should().NotThrow();
        pricing.ProfitLossRatio.Should().Be(1.6);
    }

    [Fact]
    public void CalendarSpreadPricing_ThrowsOnInvalidCost()
    {
        // Arrange
        var pricing = new CalendarSpreadPricing
        {
            FrontOption = new OptionPricing { Price = 6.00 },
            BackOption = new OptionPricing { Price = 3.50 },
            SpreadCost = -2.50 // Invalid negative cost
        };

        // Act & Assert
        pricing.Invoking(p => p.Validate())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void KellyPositionSizer_CalculatesReasonablePosition()
    {
        // Arrange
        var sizer = new KellyPositionSizer();
        var trades = GenerateProfitableTrades(30);
        var signal = new Signal
        {
            Symbol = "TEST",
            Strength = SignalStrength.Recommended,
            IVRVRatio = 1.35
        };

        // Act
        var position = sizer.CalculateFromHistory(100_000, trades, 2.50, signal);

        // Assert
        position.Contracts.Should().BeGreaterThan(0);
        position.AllocationPercent.Should().BeLessOrEqualTo(0.06);
        position.TotalRisk.Should().BeLessOrEqualTo(position.DollarAllocation);
    }

    [Fact]
    public void KellyPositionSizer_ReturnsZeroContractsForAvoidSignal()
    {
        // Arrange
        var sizer = new KellyPositionSizer();
        var trades = GenerateProfitableTrades(30);
        var signal = new Signal
        {
            Symbol = "TEST",
            Strength = SignalStrength.Avoid
        };

        // Act
        var position = sizer.CalculateFromHistory(100_000, trades, 2.50, signal);

        // Assert
        position.Contracts.Should().Be(0);
    }

    [Fact]
    public void KellyPositionSizer_HandlesInsufficientHistory()
    {
        // Arrange
        var sizer = new KellyPositionSizer();
        var trades = GenerateProfitableTrades(10); // Insufficient
        var signal = new Signal
        {
            Symbol = "TEST",
            Strength = SignalStrength.Recommended
        };

        // Act
        var position = sizer.CalculateFromHistory(100_000, trades, 2.50, signal);

        // Assert
        position.Should().NotBeNull();
        position.Contracts.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Control_EvaluatesCompleteOpportunity()
    {
        // Arrange
        var mockData = new MockMarketDataProvider();
        var yangZhang = new YangZhangEstimator();
        var termAnalyzer = new TermStructureAnalyzer();
        var signalGen = new SignalGenerator(mockData, yangZhang, termAnalyzer);
        var mockPricing = new MockPricingEngine();
        var sizer = new KellyPositionSizer();
        var control = new Control(signalGen, mockPricing, sizer);

        var trades = GenerateProfitableTrades(25);

        // Act
        var opportunity = await control.EvaluateOpportunity(
            "AAPL",
            DateTime.Today.AddDays(7),
            DateTime.Today,
            100_000,
            trades);

        // Assert
        opportunity.Should().NotBeNull();
        opportunity.Symbol.Should().Be("AAPL");
        opportunity.Signal.Should().NotBeNull();
        opportunity.SpreadPricing.Should().NotBeNull();
        opportunity.PositionSize.Should().NotBeNull();
    }

    [Fact]
    public async Task Control_SkipsOpportunityWhenSignalIsAvoid()
    {
        // Arrange
        var mockData = new MockMarketDataProvider(averageVolume: 100_000); // Will trigger Avoid
        var yangZhang = new YangZhangEstimator();
        var termAnalyzer = new TermStructureAnalyzer();
        var signalGen = new SignalGenerator(mockData, yangZhang, termAnalyzer);
        var mockPricing = new MockPricingEngine();
        var sizer = new KellyPositionSizer();
        var control = new Control(signalGen, mockPricing, sizer);

        var trades = GenerateProfitableTrades(25);

        // Act
        var opportunity = await control.EvaluateOpportunity(
            "TEST",
            DateTime.Today.AddDays(7),
            DateTime.Today,
            100_000,
            trades);

        // Assert
        opportunity.Signal?.Strength.Should().Be(SignalStrength.Avoid);
        opportunity.IsActionable.Should().BeFalse();
    }

    // Helper Methods
    private static List<PriceBar> GenerateSamplePriceBars(int count)
    {
        var random = new Random(42);
        var bars = new List<PriceBar>();
        var basePrice = 150.0;

        for (int i = 0; i < count; i++)
        {
            var open = basePrice + random.NextDouble() * 5 - 2.5;
            var close = open + random.NextDouble() * 3 - 1.5;
            var high = Math.Max(open, close) + random.NextDouble() * 2;
            var low = Math.Min(open, close) - random.NextDouble() * 2;

            bars.Add(new PriceBar
            {
                Date = DateTime.Today.AddDays(-count + i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = random.Next(1_000_000, 5_000_000)
            });

            basePrice = close;
        }

        return bars;
    }

    private static List<Trade> GenerateProfitableTrades(int count)
    {
        var random = new Random(42);
        var trades = new List<Trade>();

        for (int i = 0; i < count; i++)
        {
            var profitLoss = random.NextDouble() > 0.55 ? // 55% win rate
                random.NextDouble() * 500 + 100 : // Win
                -(random.NextDouble() * 300 + 50); // Loss

            trades.Add(new Trade
            {
                EntryDate = DateTime.Today.AddDays(-60 + i * 2),
                ExitDate = DateTime.Today.AddDays(-60 + i * 2 + 7),
                ProfitLoss = profitLoss,
                Symbol = "TEST"
            });
        }

        return trades;
    }
}

// Mock Implementations
internal class MockMarketDataProvider : IMarketDataProvider
{
    private readonly long _averageVolume;
    private readonly double _impliedVol;
    private readonly double _realizedVol;

    public MockMarketDataProvider(
        long averageVolume = 2_000_000,
        double impliedVol = 0.30,
        double realizedVol = 0.25)
    {
        _averageVolume = averageVolume;
        _impliedVol = impliedVol;
        _realizedVol = realizedVol;
    }

    public OptionChain GetOptionChain(string symbol, DateTime date)
    {
        var chain = new OptionChain
        {
            Symbol = symbol,
            UnderlyingPrice = 150.0,
            Timestamp = date
        };

        // Create inverted term structure
        var expiries = new[] { 7, 14, 30, 45 };
        var ivs = new[] { 0.35, 0.32, 0.30, 0.27 };

        for (int i = 0; i < expiries.Length; i++)
        {
            var expiry = new OptionExpiry
            {
                ExpiryDate = date.AddDays(expiries[i])
            };

            expiry.Calls.Add(new OptionContract
            {
                Strike = 150.0,
                Bid = 2.50,
                Ask = 2.60,
                ImpliedVolatility = ivs[i],
                OpenInterest = 1000,
                Volume = 500
            });

            expiry.Puts.Add(new OptionContract
            {
                Strike = 150.0,
                Bid = 2.40,
                Ask = 2.50,
                ImpliedVolatility = ivs[i] + 0.02,
                OpenInterest = 1000,
                Volume = 500
            });

            chain.Expiries.Add(expiry);
        }

        return chain;
    }

    public List<PriceBar> GetHistoricalPrices(string symbol, int days)
    {
        var bars = new List<PriceBar>();
        var random = new Random(42);
        var basePrice = 150.0;

        for (int i = 0; i < days; i++)
        {
            var open = basePrice + random.NextDouble() * 2 - 1;
            var close = open + random.NextDouble() * 2 - 1;
            var high = Math.Max(open, close) + random.NextDouble();
            var low = Math.Min(open, close) - random.NextDouble();

            bars.Add(new PriceBar
            {
                Date = DateTime.Today.AddDays(-days + i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = _averageVolume
            });

            basePrice = close;
        }

        return bars;
    }

    public double GetCurrentPrice(string symbol) => 150.0;

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
            TimeToExpiry = 0.05
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