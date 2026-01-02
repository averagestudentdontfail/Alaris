// TSUN029A.cs - Data Model and Provider Unit Tests
// Component ID: TSUN029A
//
// Tests for Alaris.Data model structures and provider interface contracts:
// - DTmd001A data models (OptionContract, PriceBar, EarningsEvent, etc.)
// - Provider interface contracts using hand-crafted mock implementations
// - Data quality validation structures
//
// Mathematical Invariants Tested:
// 1. Option Contract: Mid = (Bid + Ask) / 2, Spread = Ask - Bid
// 2. Price Bar OHLC: Low ≤ Open, Close ≤ High
// 3. Option Chain: Calls + Puts = All Contracts
// 4. Provider Contracts: Async methods return valid data
//
// References:
//   - Alaris.Governance Structure.md § 4.3.3
//   - Data quality validation patterns

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Provider;

namespace Alaris.Test.Unit;

/// <summary>
/// TSUN029A: Unit tests for data models and provider interfaces.
/// </summary>
public sealed class TSUN029A
{
    // OptionContract Tests

    /// <summary>
    /// Option contract Mid price is average of Bid and Ask.
    /// </summary>
    [Theory]
    [InlineData(1.00, 1.20, 1.10)]   // Normal spread
    [InlineData(5.50, 5.70, 5.60)]   // Typical option
    [InlineData(0.10, 0.15, 0.125)] // Low-priced option
    [InlineData(10.0, 10.0, 10.0)]  // Zero spread
    public void OptionContract_Mid_IsAverageOfBidAsk(decimal bid, decimal ask, decimal expectedMid)
    {
        // Arrange
        var contract = CreateTestOptionContract(bid: bid, ask: ask);

        // Act
        decimal mid = contract.Mid;

        // Assert
        mid.Should().Be(expectedMid);
    }

    /// <summary>
    /// Option contract Spread is Ask minus Bid.
    /// </summary>
    [Theory]
    [InlineData(1.00, 1.20, 0.20)]
    [InlineData(5.50, 5.70, 0.20)]
    [InlineData(0.10, 0.15, 0.05)]
    [InlineData(10.0, 10.0, 0.0)]
    public void OptionContract_Spread_IsAskMinusBid(decimal bid, decimal ask, decimal expectedSpread)
    {
        // Arrange
        var contract = CreateTestOptionContract(bid: bid, ask: ask);

        // Act
        decimal spread = contract.Spread;

        // Assert
        spread.Should().Be(expectedSpread);
    }

    /// <summary>
    /// Option contract bid should not exceed ask.
    /// </summary>
    [Fact]
    public void OptionContract_SpreadNonNegative_WhenValidBidAsk()
    {
        // Arrange
        var contract = CreateTestOptionContract(bid: 5.00m, ask: 5.20m);

        // Assert
        contract.Spread.Should().BeGreaterThanOrEqualTo(0);
    }

    // OptionChainSnapshot Tests

    /// <summary>
    /// Option chain Calls filter returns only calls.
    /// </summary>
    [Fact]
    public void OptionChainSnapshot_Calls_ReturnsOnlyCalls()
    {
        // Arrange
        var chain = CreateTestOptionChain(numStrikes: 5);

        // Act
        var calls = chain.Calls;

        // Assert
        calls.Should().AllSatisfy(c => c.Right.Should().Be(OptionRight.Call));
        calls.Count.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Option chain Puts filter returns only puts.
    /// </summary>
    [Fact]
    public void OptionChainSnapshot_Puts_ReturnsOnlyPuts()
    {
        // Arrange
        var chain = CreateTestOptionChain(numStrikes: 5);

        // Act
        var puts = chain.Puts;

        // Assert
        puts.Should().AllSatisfy(c => c.Right.Should().Be(OptionRight.Put));
        puts.Count.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Option chain Calls + Puts equals total contracts.
    /// </summary>
    [Fact]
    public void OptionChainSnapshot_CallsPlusPuts_EqualsTotal()
    {
        // Arrange
        var chain = CreateTestOptionChain(numStrikes: 5, numExpiries: 3);

        // Act
        int callCount = chain.Calls.Count;
        int putCount = chain.Puts.Count;
        int totalCount = chain.Contracts.Count;

        // Assert
        (callCount + putCount).Should().Be(totalCount);
    }

    /// <summary>
    /// Option chain ByExpiration groups correctly.
    /// </summary>
    [Fact]
    public void OptionChainSnapshot_ByExpiration_GroupsCorrectly()
    {
        // Arrange
        var chain = CreateTestOptionChain(numStrikes: 3, numExpiries: 4);

        // Act
        var byExpiry = chain.ByExpiration;

        // Assert
        byExpiry.Should().HaveCount(4);
        foreach (var (expiry, contracts) in byExpiry)
        {
            contracts.Should().AllSatisfy(c => c.Expiration.Should().Be(expiry));
        }
    }

    // PriceBar Tests

    /// <summary>
    /// Price bar OHLC relationship: Low ≤ O,C ≤ High.
    /// </summary>
    [Fact]
    public void PriceBar_OHLCRelationship_IsValid()
    {
        // Arrange
        var bar = new PriceBar
        {
            Symbol = "AAPL",
            Timestamp = DateTime.Today,
            Open = 150.00m,
            High = 155.00m,
            Low = 148.00m,
            Close = 152.00m,
            Volume = 1000000
        };

        // Assert
        bar.Low.Should().BeLessThanOrEqualTo(bar.Open);
        bar.Low.Should().BeLessThanOrEqualTo(bar.Close);
        bar.High.Should().BeGreaterThanOrEqualTo(bar.Open);
        bar.High.Should().BeGreaterThanOrEqualTo(bar.Close);
    }

    /// <summary>
    /// Price bar volume should be non-negative.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(100000000)]
    public void PriceBar_Volume_NonNegative(long volume)
    {
        // Arrange
        var bar = new PriceBar
        {
            Symbol = "AAPL",
            Timestamp = DateTime.Today,
            Open = 150.00m,
            High = 155.00m,
            Low = 148.00m,
            Close = 152.00m,
            Volume = volume
        };

        // Assert
        bar.Volume.Should().BeGreaterThanOrEqualTo(0);
    }

    // EarningsEvent Tests

    /// <summary>
    /// Earnings event has valid timing values.
    /// </summary>
    [Theory]
    [InlineData(EarningsTiming.BeforeMarketOpen)]
    [InlineData(EarningsTiming.AfterMarketClose)]
    [InlineData(EarningsTiming.DuringMarketHours)]
    [InlineData(EarningsTiming.Unknown)]
    public void EarningsEvent_Timing_IsValid(EarningsTiming timing)
    {
        // Arrange
        var earnings = new EarningsEvent
        {
            Symbol = "AAPL",
            Date = DateTime.Today.AddDays(7),
            Timing = timing,
            Source = "TestSource",
            FetchedAt = DateTime.UtcNow
        };

        // Assert
        earnings.Timing.Should().Be(timing);
    }

    /// <summary>
    /// Earnings event fiscal quarter format.
    /// </summary>
    [Theory]
    [InlineData("Q1")]
    [InlineData("Q2")]
    [InlineData("Q3")]
    [InlineData("Q4")]
    public void EarningsEvent_FiscalQuarter_IsValid(string quarter)
    {
        // Arrange
        var earnings = new EarningsEvent
        {
            Symbol = "AAPL",
            Date = DateTime.Today.AddDays(7),
            FiscalQuarter = quarter,
            FiscalYear = 2025,
            Source = "TestSource",
            FetchedAt = DateTime.UtcNow
        };

        // Assert
        earnings.FiscalQuarter.Should().MatchRegex(@"Q[1-4]");
    }

    // MarketDataSnapshot Tests

    /// <summary>
    /// Market data snapshot aggregates data correctly.
    /// </summary>
    [Fact]
    public void MarketDataSnapshot_ContainsAllRequiredData()
    {
        // Arrange
        var snapshot = CreateTestMarketDataSnapshot();

        // Assert
        snapshot.Symbol.Should().NotBeNullOrEmpty();
        snapshot.SpotPrice.Should().BeGreaterThan(0);
        snapshot.HistoricalBars.Should().NotBeEmpty();
        snapshot.OptionChain.Should().NotBeNull();
        snapshot.OptionChain.Contracts.Should().NotBeEmpty();
        snapshot.RiskFreeRate.Should().BeInRange(-0.05m, 0.15m);
        snapshot.DividendYield.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Market data snapshot historical bars are ordered by date.
    /// </summary>
    [Fact]
    public void MarketDataSnapshot_HistoricalBars_AreOrdered()
    {
        // Arrange
        var snapshot = CreateTestMarketDataSnapshot();

        // Assert
        for (int i = 1; i < snapshot.HistoricalBars.Count; i++)
        {
            snapshot.HistoricalBars[i].Timestamp.Should()
                .BeOnOrAfter(snapshot.HistoricalBars[i - 1].Timestamp);
        }
    }

    // DataQualityResult Tests

    /// <summary>
    /// Validation status enum values.
    /// </summary>
    [Theory]
    [InlineData(ValidationStatus.Passed)]
    [InlineData(ValidationStatus.PassedWithWarnings)]
    [InlineData(ValidationStatus.Failed)]
    public void DataQualityResult_Status_IsValid(ValidationStatus status)
    {
        // Arrange
        var result = new DataQualityResult
        {
            ValidatorId = "TEST001",
            Status = status,
            Message = "Test message",
            DataElement = "TestElement"
        };

        // Assert
        result.Status.Should().Be(status);
    }

    /// <summary>
    /// Data quality result can have warnings.
    /// </summary>
    [Fact]
    public void DataQualityResult_Warnings_CanBeProvided()
    {
        // Arrange
        var warnings = new[] { "Warning 1", "Warning 2" };
        var result = new DataQualityResult
        {
            ValidatorId = "TEST001",
            Status = ValidationStatus.PassedWithWarnings,
            Message = "Passed with warnings",
            DataElement = "TestElement",
            Warnings = warnings
        };

        // Assert
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("Warning 1");
    }

    // Provider Interface Contract Tests (with Mock Implementations)

    /// <summary>
    /// Market data provider returns historical bars.
    /// </summary>
    [Fact]
    public async Task DTpr003A_GetHistoricalBarsAsync_ReturnsBars()
    {
        // Arrange
        DTpr003A provider = new MockMarketDataProvider();

        // Act
        var bars = await provider.GetHistoricalBarsAsync(
            "AAPL",
            DateTime.Today.AddDays(-30),
            DateTime.Today);

        // Assert
        bars.Should().NotBeEmpty();
        bars.Should().AllSatisfy(b => b.Symbol.Should().Be("AAPL"));
    }

    /// <summary>
    /// Market data provider returns option chain.
    /// </summary>
    [Fact]
    public async Task DTpr003A_GetOptionChainAsync_ReturnsChain()
    {
        // Arrange
        DTpr003A provider = new MockMarketDataProvider();

        // Act
        var chain = await provider.GetOptionChainAsync("AAPL");

        // Assert
        chain.Should().NotBeNull();
        chain.Symbol.Should().Be("AAPL");
        chain.Contracts.Should().NotBeEmpty();
    }

    /// <summary>
    /// Market data provider returns valid spot price.
    /// </summary>
    [Fact]
    public async Task DTpr003A_GetSpotPriceAsync_ReturnsValidPrice()
    {
        // Arrange
        DTpr003A provider = new MockMarketDataProvider();

        // Act
        var price = await provider.GetSpotPriceAsync("AAPL");

        // Assert
        price.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Earnings provider returns upcoming earnings.
    /// </summary>
    [Fact]
    public async Task DTpr004A_GetUpcomingEarningsAsync_ReturnsEvents()
    {
        // Arrange
        DTpr004A provider = new MockEarningsProvider();

        // Act
        var events = await provider.GetUpcomingEarningsAsync("AAPL");

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(e => e.Date.Should().BeAfter(DateTime.Today));
    }

    /// <summary>
    /// Earnings provider returns historical earnings.
    /// </summary>
    [Fact]
    public async Task DTpr004A_GetHistoricalEarningsAsync_ReturnsEvents()
    {
        // Arrange
        DTpr004A provider = new MockEarningsProvider();

        // Act
        var events = await provider.GetHistoricalEarningsAsync("AAPL");

        // Assert
        events.Should().NotBeEmpty();
        events.Should().AllSatisfy(e => e.Date.Should().BeBefore(DateTime.Today.AddDays(1)));
    }

    /// <summary>
    /// Risk-free rate provider returns valid rate.
    /// </summary>
    [Fact]
    public async Task DTpr005A_GetCurrentRateAsync_ReturnsValidRate()
    {
        // Arrange
        DTpr005A provider = new MockRiskFreeRateProvider();

        // Act
        var rate = await provider.GetCurrentRateAsync();

        // Assert - Rate should be reasonable (between -5% and 15%)
        rate.Should().BeInRange(-0.05m, 0.15m);
    }

    /// <summary>
    /// Risk-free rate provider returns historical rates.
    /// </summary>
    [Fact]
    public async Task DTpr005A_GetHistoricalRatesAsync_ReturnsRates()
    {
        // Arrange
        DTpr005A provider = new MockRiskFreeRateProvider();

        // Act
        var rates = await provider.GetHistoricalRatesAsync(
            DateTime.Today.AddDays(-30),
            DateTime.Today);

        // Assert
        rates.Should().NotBeEmpty();
        rates.Values.Should().AllSatisfy(r => r.Should().BeInRange(-0.05m, 0.15m));
    }

    /// <summary>
    /// Provider cancellation token is respected.
    /// </summary>
    [Fact]
    public async Task DTpr003A_GetSpotPriceAsync_RespectsCancel()
    {
        // Arrange
        DTpr003A provider = new MockMarketDataProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.GetSpotPriceAsync("AAPL", cts.Token));
    }

    // Helper Methods

    private static OptionContract CreateTestOptionContract(
        decimal bid = 5.00m,
        decimal ask = 5.20m)
    {
        return new OptionContract
        {
            UnderlyingSymbol = "AAPL",
            OptionSymbol = "AAPL250321C00150000",
            Strike = 150.00m,
            Expiration = DateTime.Today.AddDays(30),
            Right = OptionRight.Call,
            Bid = bid,
            Ask = ask,
            Volume = 1000,
            OpenInterest = 5000,
            Timestamp = DateTime.UtcNow
        };
    }

    private static OptionChainSnapshot CreateTestOptionChain(
        int numStrikes = 5,
        int numExpiries = 2)
    {
        var contracts = new List<OptionContract>();
        decimal spotPrice = 150.00m;

        for (int e = 0; e < numExpiries; e++)
        {
            DateTime expiry = DateTime.Today.AddDays(30 * (e + 1));
            
            for (int s = 0; s < numStrikes; s++)
            {
                decimal strike = spotPrice - 10 + (s * 5);
                
                // Add call
                contracts.Add(new OptionContract
                {
                    UnderlyingSymbol = "AAPL",
                    OptionSymbol = $"AAPL{expiry:yyMMdd}C{strike * 1000:00000000}",
                    Strike = strike,
                    Expiration = expiry,
                    Right = OptionRight.Call,
                    Bid = 5.00m,
                    Ask = 5.20m,
                    Timestamp = DateTime.UtcNow
                });
                
                // Add put
                contracts.Add(new OptionContract
                {
                    UnderlyingSymbol = "AAPL",
                    OptionSymbol = $"AAPL{expiry:yyMMdd}P{strike * 1000:00000000}",
                    Strike = strike,
                    Expiration = expiry,
                    Right = OptionRight.Put,
                    Bid = 4.80m,
                    Ask = 5.00m,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return new OptionChainSnapshot
        {
            Symbol = "AAPL",
            SpotPrice = spotPrice,
            Timestamp = DateTime.UtcNow,
            Contracts = contracts
        };
    }

    private static MarketDataSnapshot CreateTestMarketDataSnapshot()
    {
        var bars = new List<PriceBar>();
        for (int i = 30; i >= 0; i--)
        {
            bars.Add(new PriceBar
            {
                Symbol = "AAPL",
                Timestamp = DateTime.Today.AddDays(-i),
                Open = 150.00m + (i % 5),
                High = 152.00m + (i % 5),
                Low = 148.00m + (i % 5),
                Close = 151.00m + (i % 5),
                Volume = 2000000 + (i * 10000)
            });
        }

        return new MarketDataSnapshot
        {
            Symbol = "AAPL",
            Timestamp = DateTime.UtcNow,
            SpotPrice = 150.00m,
            HistoricalBars = bars,
            OptionChain = CreateTestOptionChain(),
            NextEarnings = new EarningsEvent
            {
                Symbol = "AAPL",
                Date = DateTime.Today.AddDays(7),
                Source = "Test",
                FetchedAt = DateTime.UtcNow
            },
            HistoricalEarnings = new List<EarningsEvent>(),
            RiskFreeRate = 0.0525m,
            DividendYield = 0.005m,
            AverageVolume30Day = 50000000m
        };
    }
}

// Mock Provider Implementations (In-Language Mocking)

/// <summary>
/// Mock market data provider for testing.
/// </summary>
internal sealed class MockMarketDataProvider : DTpr003A
{
    public Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var bars = new List<PriceBar>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                bars.Add(new PriceBar
                {
                    Symbol = symbol,
                    Timestamp = date,
                    Open = 150.00m,
                    High = 152.00m,
                    Low = 148.00m,
                    Close = 151.00m,
                    Volume = 2000000
                });
            }
        }
        return Task.FromResult<IReadOnlyList<PriceBar>>(bars);
    }

    public Task<OptionChainSnapshot> GetOptionChainAsync(
        string symbol,
        DateTime? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var contracts = new List<OptionContract>
        {
            new OptionContract
            {
                UnderlyingSymbol = symbol,
                OptionSymbol = $"{symbol}250321C00150000",
                Strike = 150.00m,
                Expiration = DateTime.Today.AddDays(30),
                Right = OptionRight.Call,
                Bid = 5.00m,
                Ask = 5.20m,
                Timestamp = DateTime.UtcNow
            }
        };

        return Task.FromResult(new OptionChainSnapshot
        {
            Symbol = symbol,
            SpotPrice = 150.00m,
            Timestamp = DateTime.UtcNow,
            Contracts = contracts
        });
    }

    public Task<decimal> GetSpotPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(150.00m);
    }

    public Task<decimal> GetAverageVolume30DayAsync(
        string symbol,
        DateTime? evaluationDate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(50000000m);
    }
}

/// <summary>
/// Mock earnings provider for testing.
/// </summary>
internal sealed class MockEarningsProvider : DTpr004A
{
    private static readonly string[] DefaultSymbols = ["AAPL", "MSFT", "GOOGL"];
    public Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(
        string symbol,
        int daysAhead = 90,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var events = new List<EarningsEvent>
        {
            new EarningsEvent
            {
                Symbol = symbol,
                Date = DateTime.Today.AddDays(7),
                FiscalQuarter = "Q1",
                FiscalYear = 2025,
                Timing = EarningsTiming.AfterMarketClose,
                Source = "Mock",
                FetchedAt = DateTime.UtcNow
            }
        };
        return Task.FromResult<IReadOnlyList<EarningsEvent>>(events);
    }

    public Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var events = new List<EarningsEvent>();
        for (int q = 1; q <= 4; q++)
        {
            events.Add(new EarningsEvent
            {
                Symbol = symbol,
                Date = DateTime.Today.AddDays(-90 * q),
                FiscalQuarter = $"Q{5 - q}",
                FiscalYear = 2024,
                Timing = EarningsTiming.AfterMarketClose,
                EpsActual = 1.50m + (q * 0.10m),
                Source = "Mock",
                FetchedAt = DateTime.UtcNow
            });
        }
        return Task.FromResult<IReadOnlyList<EarningsEvent>>(events);
    }

    public Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        DateTime anchorDate,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default)
        => GetHistoricalEarningsAsync(symbol, lookbackDays, cancellationToken);

    public Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(DefaultSymbols);
    }

    public void EnableCacheOnlyMode() { }
}

/// <summary>
/// Mock risk-free rate provider for testing.
/// </summary>
internal sealed class MockRiskFreeRateProvider : DTpr005A
{
    public Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0.0525m);  // 5.25%
    }

    public Task<IReadOnlyDictionary<DateTime, decimal>> GetHistoricalRatesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var rates = new Dictionary<DateTime, decimal>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                rates[date] = 0.0525m;  // Constant rate for simplicity
            }
        }
        return Task.FromResult<IReadOnlyDictionary<DateTime, decimal>>(rates);
    }
}
