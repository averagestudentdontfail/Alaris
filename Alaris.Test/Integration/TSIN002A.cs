using Xunit;
using FluentAssertions;
using Alaris.Strategy;
using Alaris.Strategy.Core;
using Alaris.Strategy.Bridge;
using Alaris.Strategy.Model;
using Alaris.Strategy.Pricing;
using Alaris.Strategy.Risk;
using Alaris.Core.Time;
using Alaris.Core.Options;

namespace Alaris.Test.Integration;

/// <summary>
/// Integration tests for the earnings volatility calendar spread strategy.
/// Tests the complete workflow from signal generation to position sizing.
/// </summary>
public class StrategyIntegrationTests
{
    [Fact]
    public void STCR003AEstimator_CalculatesVolatilityCorrectly()
    {
        // Arrange
        var estimator = new STCR003AEstimator();
        var priceBars = GenerateSamplePriceBars(50);

        // Act
        var volatility = estimator.Calculate(priceBars, 30, annualized: true);

        // Assert
        volatility.Should().BeGreaterThan(0);
        volatility.Should().BeLessThan(2.0);
    }

    [Fact]
    public void STCR003AEstimator_CalculatesRollingVolatility()
    {
        // Arrange
        var estimator = new STCR003AEstimator();
        var priceBars = GenerateSamplePriceBars(100);

        // Act
        var rollingVol = estimator.CalculateRolling(priceBars, 30, annualized: true);

        // Assert
        rollingVol.Should().NotBeEmpty();
        rollingVol.Should().HaveCount(100 - 30);
        rollingVol.All(v => v.Volatility > 0).Should().BeTrue();
    }

    [Fact]
    public void STTM001AAnalyzer_IdentifiesInvertedStructure()
    {
        // Arrange
        var analyzer = new STTM001AAnalyzer();
        
        // Use steeper inversion to meet -0.00406 threshold (from Atilgan 2014)
        var points = new List<STTM001APoint>
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
    public void STTM001AAnalyzer_IdentifiesNormalStructure()
    {
        // Arrange
        var analyzer = new STTM001AAnalyzer();
        var points = new List<STTM001APoint>
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
    public void STCR001A_GeneratesSTCR004A()
    {
        // Arrange
        var mockMarketData = new MockMarketDataProvider();
        var yangZhang = new STCR003AEstimator();
        var termAnalyzer = new STTM001AAnalyzer();
        var generator = new STCR001A(mockMarketData, yangZhang, termAnalyzer);

        var earningsDate = new DateTime(2024, 1, 25);
        var evaluationDate = new DateTime(2024, 1, 24);

        // Act
        var signal = generator.Generate("AAPL", earningsDate, evaluationDate);

        // Assert
        signal.Should().NotBeNull();
        signal.Symbol.Should().Be("AAPL");
        signal.Strength.Should().BeOneOf(
            STCR004AStrength.Avoid,
            STCR004AStrength.Consider,
            STCR004AStrength.Recommended);
    }

    [Fact]
    public void STRK001A_CalculatesPosition()
    {
        // Arrange
        var sizer = new STRK001A();
        var historicalTrades = GenerateSampleTrades(30);
        var portfolioValue = 100000.0;
        var spreadCost = 2.50;
        var signal = new STCR004A
        {
            Symbol = "AAPL",
            Strength = STCR004AStrength.Recommended,
            IVRVRatio = 1.30,
            STTM001ASlope = -0.005
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
    public void STPR001APricing_ValidatesCorrectly()
    {
        // Arrange
        var pricing = new STPR001APricing
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
    public async Task STCT001A_EvaluatesOpportunity()
    {
        // Arrange
        var mockMarketData = new MockMarketDataProvider();
        var yangZhang = new STCR003AEstimator();
        var termAnalyzer = new STTM001AAnalyzer();
        var signalGenerator = new STCR001A(mockMarketData, yangZhang, termAnalyzer);
        var mockPricing = new MockPricingEngine();
        var sizer = new STRK001A();
        var control = new STCT001A(signalGenerator, mockPricing, sizer);

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
        opportunity.STCR004A.Should().NotBeNull();
    }

    [Fact]
    public async Task STBR001A_IntegrationTest_PositiveRates()
    {
        // Arrange - Use native types
        var valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using var engine = new STBR001A();

        var parameters = new STDT003As
        {
            UnderlyingPrice = 150.0,
            Strike = 150.0,
            Expiry = valuationDate.AddDays(30),
            ImpliedVolatility = 0.30,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = OptionType.Call,
            ValuationDate = valuationDate
        };

        // Act
        var result = await engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(0, 1);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task STBR001A_IntegrationTest_NegativeRates()
    {
        // Arrange: Healy (2021) parameters using native types
        var valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using var engine = new STBR001A();

        var parameters = new STDT003As
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            Expiry = valuationDate.AddDays(365),
            ImpliedVolatility = 0.08,
            RiskFreeRate = -0.005,
            DividendYield = -0.010,
            OptionType = OptionType.Put,
            ValuationDate = valuationDate
        };

        // Act
        var result = await engine.PriceOption(parameters);

        // Assert
        result.Should().NotBeNull();
        result.Price.Should().BeGreaterThan(0);
        result.Delta.Should().BeInRange(-1, 0);
        result.Gamma.Should().BeGreaterThan(0);
        result.Vega.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task STBR001A_STPR001A_PositiveRates()
    {
        // Arrange - Use native types
        var valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using var engine = new STBR001A();

        var parameters = new STPR001AParameters
        {
            UnderlyingPrice = 150.0,
            Strike = 150.0,
            FrontExpiry = valuationDate.AddDays(30),
            BackExpiry = valuationDate.AddDays(60),
            ImpliedVolatility = 0.30,
            RiskFreeRate = 0.05,
            DividendYield = 0.02,
            OptionType = OptionType.Call,
            ValuationDate = valuationDate
        };

        // Act
        var result = await engine.PriceSTPR001A(parameters);

        // Assert
        result.Should().NotBeNull();
        result.SpreadCost.Should().BeGreaterThan(0);
        result.BackOption.Price.Should().BeGreaterThan(result.FrontOption.Price);
        result.SpreadVega.Should().BeGreaterThan(0);
        result.ProfitLossRatio.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public async Task STBR001A_STPR001A_NegativeRates()
    {
        // Arrange - Use native types
        var valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using var engine = new STBR001A();

        var parameters = new STPR001AParameters
        {
            UnderlyingPrice = 100.0,
            Strike = 100.0,
            FrontExpiry = valuationDate.AddDays(30),
            BackExpiry = valuationDate.AddDays(60),
            ImpliedVolatility = 0.08,
            RiskFreeRate = -0.005,
            DividendYield = -0.010,
            OptionType = OptionType.Put,
            ValuationDate = valuationDate
        };

        // Act
        var result = await engine.PriceSTPR001A(parameters);

        // Assert
        result.Should().NotBeNull();
        result.SpreadCost.Should().BeGreaterThan(0);
        result.BackOption.Price.Should().BeGreaterThan(result.FrontOption.Price);
        result.SpreadVega.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task STCT001A_WithSTBR001A_FullWorkflow()
    {
        // Arrange - Use native types
        var mockMarketData = new MockMarketDataProvider();
        var yangZhang = new STCR003AEstimator();
        var termAnalyzer = new STTM001AAnalyzer();
        var signalGenerator = new STCR001A(mockMarketData, yangZhang, termAnalyzer);

        // Use real STBR001A instead of mock
        using var pricingEngine = new STBR001A();
        var sizer = new STRK001A();
        var control = new STCT001A(signalGenerator, pricingEngine, sizer);

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
        opportunity.STCR004A.Should().NotBeNull();

        if (opportunity.SpreadPricing != null)
        {
            opportunity.SpreadPricing.SpreadCost.Should().BeGreaterThan(0);
            opportunity.SpreadPricing.BackOption.Price.Should().BeGreaterThan(0);
            opportunity.SpreadPricing.FrontOption.Price.Should().BeGreaterThan(0);
        }
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
            var high = System.Math.Max(open, close) + random.NextDouble() * 2;
            var low = System.Math.Min(open, close) - random.NextDouble() * 2;

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
internal class MockMarketDataProvider : STDT001A
{
    public STDT002A GetSTDT002A(string symbol, DateTime date)
    {
        var chain = new STDT002A
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

    public IReadOnlyList<PriceBar> GetHistoricalPrices(string symbol, int days)
    {
        var bars = new List<PriceBar>();
        var random = new Random(42);
        var basePrice = 150.0;

        for (int i = 0; i < days; i++)
        {
            var open = basePrice + random.NextDouble() * 2 - 1;
            var close = open + random.NextDouble() * 2 - 1;
            var high = System.Math.Max(open, close) + random.NextDouble();
            var low = System.Math.Min(open, close) - random.NextDouble();

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

    public Task<IReadOnlyList<DateTime>> GetEarningsDates(string symbol)
    {
        return Task.FromResult<IReadOnlyList<DateTime>>(new List<DateTime> { DateTime.Today.AddDays(7) });
    }

    public Task<IReadOnlyList<DateTime>> GetHistoricalEarningsDates(string symbol, int lookbackQuarters = 12)
    {
        var historicalDates = new List<DateTime>();
        for (int i = 0; i < lookbackQuarters; i++)
        {
            historicalDates.Add(DateTime.Today.AddDays(-90 * (i + 1)));
        }
        return Task.FromResult<IReadOnlyList<DateTime>>(historicalDates);
    }

    public Task<bool> IsDataAvailable(string symbol)
    {
        return Task.FromResult(true);
    }
}

internal class MockPricingEngine : STBR002A
{
    public Task<OptionPricing> PriceOption(STDT003As parameters)
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

    public Task<STPR001APricing> PriceSTPR001A(STPR001AParameters parameters)
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

        return Task.FromResult(new STPR001APricing
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

    public Task<double> CalculateImpliedVolatility(double marketPrice, STDT003As parameters)
    {
        return Task.FromResult(0.30);
    }
}