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
    private const double MinIvRvRatio = 1.25;
    private const double MaxTermSlope = -0.00406;
    private const long MinimumAverageVolume = 1_500_000;

    [Fact]
    public void STCR003A_CalculatesVolatilityCorrectly()
    {
        // Arrange
        STCR003A estimator = new STCR003A();
        List<PriceBar> priceBars = GenerateSamplePriceBars(50);

        // Act
        double volatility = estimator.Calculate(priceBars, 30, annualized: true);

        // Assert
        volatility.Should().BeGreaterThan(0);
        volatility.Should().BeLessThan(2.0);
    }

    [Fact]
    public void STCR003A_CalculatesRollingVolatility()
    {
        // Arrange
        STCR003A estimator = new STCR003A();
        List<PriceBar> priceBars = GenerateSamplePriceBars(100);

        // Act
        IReadOnlyList<(DateTime Date, double Volatility)> rollingVol = estimator.CalculateRolling(priceBars, 30, annualized: true);

        // Assert
        rollingVol.Should().NotBeEmpty();
        rollingVol.Should().HaveCount(100 - 30);
        bool allPositive = true;
        for (int i = 0; i < rollingVol.Count; i++)
        {
            if (rollingVol[i].Volatility <= 0)
            {
                allPositive = false;
                break;
            }
        }
        allPositive.Should().BeTrue();
    }

    [Fact]
    public void STTM001A_IdentifiesInvertedStructure()
    {
        // Arrange
        STTM001A analyzer = new STTM001A();
        
        // Use steeper inversion to meet -0.00406 threshold (from Atilgan 2014)
        List<STTM001APoint> points = new List<STTM001APoint>
        {
            new STTM001APoint { DaysToExpiry = 10, ImpliedVolatility = 0.45, Strike = 100 },
            new STTM001APoint { DaysToExpiry = 20, ImpliedVolatility = 0.37, Strike = 100 },
            new STTM001APoint { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
            new STTM001APoint { DaysToExpiry = 45, ImpliedVolatility = 0.23, Strike = 100 }
        };

        // Act
        STTM001AAnalysis analysis = analyzer.Analyze(points);

        // Assert
        analysis.IsInverted.Should().BeTrue();
        analysis.Slope.Should().BeLessThan(0);
        analysis.MeetsTradingCriterion.Should().BeTrue();
    }

    [Fact]
    public void STTM001A_IdentifiesNormalStructure()
    {
        // Arrange
        STTM001A analyzer = new STTM001A();
        List<STTM001APoint> points = new List<STTM001APoint>
        {
            new STTM001APoint { DaysToExpiry = 10, ImpliedVolatility = 0.25, Strike = 100 },
            new STTM001APoint { DaysToExpiry = 20, ImpliedVolatility = 0.28, Strike = 100 },
            new STTM001APoint { DaysToExpiry = 30, ImpliedVolatility = 0.30, Strike = 100 },
            new STTM001APoint { DaysToExpiry = 45, ImpliedVolatility = 0.32, Strike = 100 }
        };

        // Act
        STTM001AAnalysis analysis = analyzer.Analyze(points);

        // Assert
        analysis.IsInverted.Should().BeFalse();
        analysis.Slope.Should().BeGreaterThan(0);
        analysis.MeetsTradingCriterion.Should().BeFalse();
    }

    [Fact]
    public void STCR001A_GeneratesSTCR004A()
    {
        // Arrange
        MockMarketDataProvider mockMarketData = new MockMarketDataProvider();
        STCR003A yangZhang = new STCR003A();
        STTM001A termAnalyzer = new STTM001A();
        STCR001A generator = new STCR001A(
            mockMarketData,
            yangZhang,
            termAnalyzer,
            MinIvRvRatio,
            MaxTermSlope,
            MinimumAverageVolume);

        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime evaluationDate = new DateTime(2024, 1, 24);

        // Act
        STCR004A signal = generator.Generate("AAPL", earningsDate, evaluationDate);

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
        STRK001A sizer = new STRK001A();
        List<Trade> historicalTrades = GenerateSampleTrades(30);
        double portfolioValue = 100000.0;
        double spreadCost = 2.50;
        STCR004A signal = new STCR004A
        {
            Symbol = "AAPL",
            Strength = STCR004AStrength.Recommended,
            IVRVRatio = 1.30,
            STTM001ASlope = -0.005
        };

        // Act
        STRK002A position = sizer.CalculateFromHistory(
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
        STPR001APricing pricing = new STPR001APricing
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
        MockMarketDataProvider mockMarketData = new MockMarketDataProvider();
        STCR003A yangZhang = new STCR003A();
        STTM001A termAnalyzer = new STTM001A();
        STCR001A signalGenerator = new STCR001A(
            mockMarketData,
            yangZhang,
            termAnalyzer,
            MinIvRvRatio,
            MaxTermSlope,
            MinimumAverageVolume);
        MockPricingEngine mockPricing = new MockPricingEngine();
        STRK001A sizer = new STRK001A();
        STCT001A control = new STCT001A(signalGenerator, mockPricing, sizer);

        List<Trade> historicalTrades = GenerateSampleTrades(25);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime evaluationDate = new DateTime(2024, 1, 24);

        // Act
        TradingOpportunity opportunity = await control.EvaluateOpportunity(
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
        CRTM005A valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using STBR001A engine = new STBR001A();

        STDT003A parameters = new STDT003A
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
        OptionPricing result = await engine.PriceOption(parameters);

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
        CRTM005A valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using STBR001A engine = new STBR001A();

        STDT003A parameters = new STDT003A
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
        OptionPricing result = await engine.PriceOption(parameters);

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
        CRTM005A valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using STBR001A engine = new STBR001A();

        STPR001AParameters parameters = new STPR001AParameters
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
        STPR001APricing result = await engine.PriceSTPR001A(parameters);

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
        CRTM005A valuationDate = new CRTM005A(15, CRTM005AMonth.January, 2024);

        using STBR001A engine = new STBR001A();

        STPR001AParameters parameters = new STPR001AParameters
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
        STPR001APricing result = await engine.PriceSTPR001A(parameters);

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
        MockMarketDataProvider mockMarketData = new MockMarketDataProvider();
        STCR003A yangZhang = new STCR003A();
        STTM001A termAnalyzer = new STTM001A();
        STCR001A signalGenerator = new STCR001A(
            mockMarketData,
            yangZhang,
            termAnalyzer,
            MinIvRvRatio,
            MaxTermSlope,
            MinimumAverageVolume);

        // Use real STBR001A instead of mock
        using STBR001A pricingEngine = new STBR001A();
        STRK001A sizer = new STRK001A();
        STCT001A control = new STCT001A(signalGenerator, pricingEngine, sizer);

        List<Trade> historicalTrades = GenerateSampleTrades(25);
        DateTime earningsDate = new DateTime(2024, 1, 25);
        DateTime evaluationDate = new DateTime(2024, 1, 24);

        // Act
        TradingOpportunity opportunity = await control.EvaluateOpportunity(
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
        Random random = new Random(42);
        List<PriceBar> bars = new List<PriceBar>();
        double basePrice = 150.0;

        for (int i = 0; i < count; i++)
        {
            double open = basePrice + random.NextDouble() * 5 - 2.5;
            double close = open + random.NextDouble() * 3 - 1.5;
            double high = System.Math.Max(open, close) + random.NextDouble() * 2;
            double low = System.Math.Min(open, close) - random.NextDouble() * 2;

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
        Random random = new Random(42);
        List<Trade> trades = new List<Trade>();

        for (int i = 0; i < count; i++)
        {
            double profitLoss = random.NextDouble() > 0.55 ? // 55% win rate
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
        STDT002A chain = new STDT002A
        {
            Symbol = symbol,
            UnderlyingPrice = 150.0,
            Timestamp = date
        };

        // Add sample expiries
        OptionExpiry expiry1 = new OptionExpiry
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
        OptionExpiry expiry2 = new OptionExpiry
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
        List<PriceBar> bars = new List<PriceBar>();
        Random random = new Random(42);
        double basePrice = 150.0;

        for (int i = 0; i < days; i++)
        {
            double open = basePrice + random.NextDouble() * 2 - 1;
            double close = open + random.NextDouble() * 2 - 1;
            double high = System.Math.Max(open, close) + random.NextDouble();
            double low = System.Math.Min(open, close) - random.NextDouble();

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
        List<DateTime> historicalDates = new List<DateTime>();
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
    public Task<OptionPricing> PriceOption(STDT003A parameters)
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
        OptionPricing frontOption = new OptionPricing
        {
            Price = 3.00,
            Delta = 0.50,
            Vega = 0.10,
            Theta = -0.05
        };

        OptionPricing backOption = new OptionPricing
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

    public Task<double> CalculateImpliedVolatility(double marketPrice, STDT003A parameters)
    {
        return Task.FromResult(0.30);
    }
}
