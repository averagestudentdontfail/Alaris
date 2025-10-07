using Xunit;
using FluentAssertions;
using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;
using Alaris.Strategy.Risk;

namespace Alaris.Strategy.Test;

public class StrategyTests
{
    [Fact]
    public void YangZhangEstimator_CalculatesVolatility()
    {
        // Arrange
        var estimator = new YangZhangEstimator();
        var priceBars = GenerateSamplePriceBars(50);

        // Act
        var volatility = estimator.Calculate(priceBars, 30, annualized: true);

        // Assert
        volatility.Should().BeGreaterThan(0);
        volatility.Should().BeLessThan(2.0); // Reasonable bounds for annualized vol
    }

    [Fact]
    public void TermStructure_CalculatesSlope()
    {
        // Arrange
        var termAnalyzer = new TermStructureAnalyzer();
        var points = new List<TermStructurePoint>
        {
            new() { DaysToExpiry = 10, ImpliedVolatility = 0.35, Strike = 100 },
            new() { DaysToExpiry = 20, ImpliedVolatility = 0.32, Strike = 100 },
            new() { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
            new() { DaysToExpiry = 45, ImpliedVolatility = 0.28, Strike = 100 }
        };

        // Act
        var analysis = termAnalyzer.Analyze(points);

        // Assert
        analysis.Slope.Should().BeLessThan(0); // Inverted term structure
        analysis.IsInverted.Should().BeTrue();
    }

    [Fact]
    public void SignalGenerator_GeneratesSignal()
    {
        // Arrange
        var mockMarketData = new MockMarketDataProvider();
        var yangZhang = new YangZhangEstimator();
        var termAnalyzer = new TermStructureAnalyzer();
        var generator = new SignalGenerator(mockMarketData, yangZhang, termAnalyzer);

        var earningsDate = new DateTime(2024, 1, 25);
        var evaluationDate = new DateTime(2024, 1, 24);

        // Act
        var signal = generator.Generate("AAPL", earningsDate, evaluationDate);

        // Assert
        signal.Should().NotBeNull();
        signal.Symbol.Should().Be("AAPL");
        signal.Strength.Should().BeOneOf(
            SignalStrength.Avoid,
            SignalStrength.Consider,
            SignalStrength.Recommended);
    }

    [Fact]
    public void KellyPositionSizer_CalculatesPosition()
    {
        // Arrange
        var sizer = new KellyPositionSizer();
        var historicalTrades = GenerateSampleTrades(30);
        var portfolioValue = 100000.0;
        var spreadCost = 2.50;
        var signal = new Signal
        {
            Symbol = "AAPL",
            Strength = SignalStrength.Recommended,
            IVRVRatio = 1.30,
            TermStructureSlope = -0.005
        };

        // Act
        var position = sizer.CalculateFromHistory(
            portfolioValue,
            historicalTrades,
            spreadCost,
            signal);

        // Assert
        position.Should().NotBeNull();
        position.Contracts.Should().BeGreaterThanOrEqualTo(0);
        position.AllocationPercent.Should().BeLessOrEqualTo(0.06); // Max 6%
    }

    [Fact]
    public void CalendarSpreadPricing_ValidatesCorrectly()
    {
        // Arrange
        var pricing = new CalendarSpreadPricing
        {
            FrontOption = new OptionPricing { Price = 3.00, Delta = 0.50 },
            BackOption = new OptionPricing { Price = 5.50, Delta = 0.45 },
            SpreadCost = 2.50,
            MaxProfit = 5.00,
            MaxLoss = 2.50
        };

        // Act & Assert
        pricing.Invoking(p => p.Validate()).Should().NotThrow();
        pricing.ProfitLossRatio.Should().Be(2.0);
    }

    [Fact]
    public async Task Control_EvaluatesOpportunity()
    {
        // Arrange
        var mockMarketData = new MockMarketDataProvider();
        var yangZhang = new YangZhangEstimator();
        var termAnalyzer = new TermStructureAnalyzer();
        var signalGenerator = new SignalGenerator(mockMarketData, yangZhang, termAnalyzer);
        var mockPricing = new MockPricingEngine();
        var sizer = new KellyPositionSizer();
        var control = new Control(signalGenerator, mockPricing, sizer);

        var historicalTrades = GenerateSampleTrades(25);
        var earningsDate = new DateTime(2024, 1, 25);
        var evaluationDate = new DateTime(2024, 1, 24);

        // Act
        var opportunity = await control.EvaluateOpportunity(
            "AAPL",
            earningsDate,
            evaluationDate,
            100000.0,
            historicalTrades);

        // Assert
        opportunity.Should().NotBeNull();
        opportunity.Symbol.Should().Be("AAPL");
        opportunity.Signal.Should().NotBeNull();
    }

    // Helper methods
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

    private static List<Trade> GenerateSampleTrades(int count)
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

// Mock implementations for testing
internal class MockMarketDataProvider : IMarketDataProvider
{
    public OptionChain GetOptionChain(string symbol, DateTime date)
    {
        var chain = new OptionChain
        {
            Symbol = symbol,
            UnderlyingPrice = 150.0,
            Timestamp = date
        };

        // Add sample expiries
        var expiry1 = new OptionExpiry
        {
            ExpiryDate = date.AddDays(7)
        };

        // Add ATM call and put
        expiry1.Calls.Add(new OptionContract
        {
            Strike = 150.0,
            Bid = 2.50,
            Ask = 2.60,
            ImpliedVolatility = 0.30,
            OpenInterest = 1000,
            Volume = 500
        });

        expiry1.Puts.Add(new OptionContract
        {
            Strike = 150.0,
            Bid = 2.40,
            Ask = 2.50,
            ImpliedVolatility = 0.32,
            OpenInterest = 1000,
            Volume = 500
        });

        chain.Expiries.Add(expiry1);

        // Add second expiry
        var expiry2 = new OptionExpiry
        {
            ExpiryDate = date.AddDays(35)
        };

        expiry2.Calls.Add(new OptionContract
        {
            Strike = 150.0,
            Bid = 5.00,
            Ask = 5.10,
            ImpliedVolatility = 0.28,
            OpenInterest = 800,
            Volume = 300
        });

        expiry2.Puts.Add(new OptionContract
        {
            Strike = 150.0,
            Bid = 4.90,
            Ask = 5.00,
            ImpliedVolatility = 0.29,
            OpenInterest = 800,
            Volume = 300
        });

        chain.Expiries.Add(expiry2);

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
                Volume = 2_000_000
            });

            basePrice = close;
        }

        return bars;
    }

    public double GetCurrentPrice(string symbol) => 150.0;

    public Task<List<DateTime>> GetEarningsDates(string symbol)
    {
        return Task.FromResult(new List<DateTime> { DateTime.Today.AddDays(7) });
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
        var frontOption = new OptionPricing
        {
            Price = 3.00,
            Delta = 0.50,
            Vega = 0.10,
            Theta = -0.05
        };

        var backOption = new OptionPricing
        {
            Price = 5.50,
            Delta = 0.45,
            Vega = 0.20,
            Theta = -0.02
        };

        return Task.FromResult(new CalendarSpreadPricing
        {
            FrontOption = frontOption,
            BackOption = backOption,
            SpreadCost = 2.50,
            SpreadDelta = backOption.Delta - frontOption.Delta,
            SpreadVega = backOption.Vega - frontOption.Vega,
            SpreadTheta = backOption.Theta - frontOption.Theta,
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