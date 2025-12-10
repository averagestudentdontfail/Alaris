// =============================================================================
// DataBridgeTests.cs - Integration Tests for AlarisDataBridge (DTbr001A)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Alaris.Data.Bridge;
using Alaris.Data.Model;
using Alaris.Data.Provider;
using Alaris.Data.Quality;

namespace Alaris.Test.Integration;

/// <summary>
/// Integration tests for AlarisDataBridge (DTbr001A).
/// Uses mock providers to test data aggregation and validation pipeline.
/// </summary>
public sealed class DTbr001ATests
{
    private readonly ILogger<AlarisDataBridge> _logger;

    public DTbr001ATests()
    {
        _logger = new LoggerFactory().CreateLogger<AlarisDataBridge>();
    }

    [Fact]
    public async Task GetMarketDataSnapshotAsync_WithValidData_ReturnsSnapshot()
    {
        // Arrange
        var marketDataProvider = new BridgeTestMarketDataProvider();
        var earningsProvider = new BridgeTestEarningsProvider();
        var riskFreeRateProvider = new BridgeTestRiskFreeRateProvider();
        var validators = new DTqc002A[]
        {
            new BridgeTestPassingValidator()
        };

        var bridge = new AlarisDataBridge(
            marketDataProvider,
            earningsProvider,
            riskFreeRateProvider,
            validators,
            _logger);

        // Act
        var snapshot = await bridge.GetMarketDataSnapshotAsync("AAPL");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Symbol.Should().Be("AAPL");
        snapshot.SpotPrice.Should().Be(152.9m);
        snapshot.RiskFreeRate.Should().Be(0.0525m);
        snapshot.HistoricalBars.Should().HaveCount(30);
        snapshot.OptionChain.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMarketDataSnapshotAsync_WithValidationFailure_ThrowsException()
    {
        // Arrange
        var marketDataProvider = new BridgeTestMarketDataProvider();
        var earningsProvider = new BridgeTestEarningsProvider();
        var riskFreeRateProvider = new BridgeTestRiskFreeRateProvider();
        var validators = new DTqc002A[]
        {
            new BridgeTestFailingValidator()
        };

        var bridge = new AlarisDataBridge(
            marketDataProvider,
            earningsProvider,
            riskFreeRateProvider,
            validators,
            _logger);

        // Act
        Func<Task> act = async () => await bridge.GetMarketDataSnapshotAsync("AAPL");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Data quality validation failed*");
    }

    [Fact]
    public async Task GetMarketDataSnapshotAsync_AggregatesDataConcurrently()
    {
        // Arrange
        var marketDataProvider = new BridgeTestMarketDataProviderWithDelay(TimeSpan.FromMilliseconds(50));
        var earningsProvider = new BridgeTestEarningsProviderWithDelay(TimeSpan.FromMilliseconds(50));
        var riskFreeRateProvider = new BridgeTestRiskFreeRateProviderWithDelay(TimeSpan.FromMilliseconds(50));
        var validators = new DTqc002A[] { new BridgeTestPassingValidator() };

        var bridge = new AlarisDataBridge(
            marketDataProvider,
            earningsProvider,
            riskFreeRateProvider,
            validators,
            _logger);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = await bridge.GetMarketDataSnapshotAsync("AAPL");
        sw.Stop();

        // Assert - Concurrent execution should complete faster than sequential (150ms vs 450ms+)
        sw.ElapsedMilliseconds.Should().BeLessThan(300, "data fetching should be concurrent");
        snapshot.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMarketDataSnapshotAsync_IncludesEarningsData()
    {
        // Arrange
        var marketDataProvider = new BridgeTestMarketDataProvider();
        var earningsProvider = new BridgeTestEarningsProvider();
        var riskFreeRateProvider = new BridgeTestRiskFreeRateProvider();
        var validators = new DTqc002A[] { new BridgeTestPassingValidator() };

        var bridge = new AlarisDataBridge(
            marketDataProvider,
            earningsProvider,
            riskFreeRateProvider,
            validators,
            _logger);

        // Act
        var snapshot = await bridge.GetMarketDataSnapshotAsync("AAPL");

        // Assert
        snapshot.NextEarnings.Should().NotBeNull();
        snapshot.NextEarnings!.Symbol.Should().Be("AAPL");
        snapshot.HistoricalEarnings.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Constructor_NullMarketDataProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var earningsProvider = new BridgeTestEarningsProvider();
        var riskFreeRateProvider = new BridgeTestRiskFreeRateProvider();
        var validators = new DTqc002A[] { new BridgeTestPassingValidator() };

        // Act & Assert
        var act = () => new AlarisDataBridge(
            null!,
            earningsProvider,
            riskFreeRateProvider,
            validators,
            _logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("marketDataProvider");
    }

    [Fact]
    public void Constructor_NullEarningsProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var marketDataProvider = new BridgeTestMarketDataProvider();
        var riskFreeRateProvider = new BridgeTestRiskFreeRateProvider();
        var validators = new DTqc002A[] { new BridgeTestPassingValidator() };

        // Act & Assert
        var act = () => new AlarisDataBridge(
            marketDataProvider,
            null!,
            riskFreeRateProvider,
            validators,
            _logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("earningsProvider");
    }

    [Fact]
    public void Constructor_NullRiskFreeRateProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var marketDataProvider = new BridgeTestMarketDataProvider();
        var earningsProvider = new BridgeTestEarningsProvider();
        var validators = new DTqc002A[] { new BridgeTestPassingValidator() };

        // Act & Assert
        var act = () => new AlarisDataBridge(
            marketDataProvider,
            earningsProvider,
            null!,
            validators,
            _logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("riskFreeRateProvider");
    }
}

// =============================================================================
// Mock Implementations for Data Bridge Testing
// =============================================================================

internal class BridgeTestMarketDataProvider : DTpr003A
{
    public virtual Task<decimal> GetSpotPriceAsync(string symbol, CancellationToken cancellationToken = default)
        => Task.FromResult(150.00m);

    public virtual Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var bars = new List<PriceBar>();
        for (int i = 0; i < 30; i++)
        {
            bars.Add(new PriceBar
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow.AddDays(-30 + i),
                Open = 148m + i * 0.1m,
                High = 152m + i * 0.1m,
                Low = 147m + i * 0.1m,
                Close = 150m + i * 0.1m,
                Volume = 5_000_000
            });
        }
        return Task.FromResult<IReadOnlyList<PriceBar>>(bars);
    }

    public virtual Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        var timestamp = asOfDate ?? DateTime.UtcNow;
        return Task.FromResult(new OptionChainSnapshot
        {
            Symbol = symbol,
            SpotPrice = 150.00m,
            Timestamp = timestamp,
            Contracts = new List<OptionContract>
            {
                new()
                {
                    UnderlyingSymbol = symbol,
                    OptionSymbol = $"{symbol}250117C00150000",
                    Strike = 150m,
                    Expiration = timestamp.AddDays(30),
                    Right = OptionRight.Call,
                    Bid = 5.00m,
                    Ask = 5.20m,
                    Volume = 1000,
                    OpenInterest = 5000,
                    ImpliedVolatility = 0.25m,
                    Timestamp = timestamp
                }
            }
        });
    }

    public virtual Task<decimal> GetAverageVolume30DayAsync(string symbol, DateTime? evaluationDate = null, CancellationToken cancellationToken = default)
        => Task.FromResult(5_000_000m);
}

internal class BridgeTestMarketDataProviderWithDelay : BridgeTestMarketDataProvider
{
    private readonly TimeSpan _delay;

    public BridgeTestMarketDataProviderWithDelay(TimeSpan delay) => _delay = delay;

    public override async Task<decimal> GetSpotPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return 150.00m;
    }

    public override async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return await base.GetHistoricalBarsAsync(symbol, startDate, endDate, cancellationToken);
    }

    public override async Task<OptionChainSnapshot> GetOptionChainAsync(string symbol, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return await base.GetOptionChainAsync(symbol, asOfDate, cancellationToken);
    }

    public override async Task<decimal> GetAverageVolume30DayAsync(string symbol, DateTime? evaluationDate = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return 5_000_000m;
    }
}

internal class BridgeTestEarningsProvider : DTpr004A
{
    private static readonly IReadOnlyList<string> s_defaultSymbols = new[] { "AAPL", "MSFT", "GOOGL" };

    public virtual Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(string symbol, int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var earnings = new List<EarningsEvent>
        {
            new()
            {
                Symbol = symbol,
                Date = DateTime.UtcNow.Date.AddDays(30),
                FiscalQuarter = "Q1",
                FiscalYear = 2025,
                Timing = EarningsTiming.AfterMarketClose,
                Source = "Mock",
                FetchedAt = DateTime.UtcNow
            }
        };
        return Task.FromResult<IReadOnlyList<EarningsEvent>>(earnings);
    }

    public virtual Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(string symbol, int lookbackDays = 730, CancellationToken cancellationToken = default)
    {
        var earnings = new List<EarningsEvent>
        {
            new()
            {
                Symbol = symbol,
                Date = DateTime.UtcNow.Date.AddDays(-90),
                FiscalQuarter = "Q4",
                FiscalYear = 2024,
                Timing = EarningsTiming.AfterMarketClose,
                Source = "Mock",
                FetchedAt = DateTime.UtcNow.AddDays(-90)
            }
        };
        return Task.FromResult<IReadOnlyList<EarningsEvent>>(earnings);
    }

    public virtual Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(s_defaultSymbols);
    }
}

internal class BridgeTestEarningsProviderWithDelay : BridgeTestEarningsProvider
{
    private readonly TimeSpan _delay;

    public BridgeTestEarningsProviderWithDelay(TimeSpan delay) => _delay = delay;

    public override async Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(string symbol, int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return await base.GetUpcomingEarningsAsync(symbol, daysAhead, cancellationToken);
    }

    public override async Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(string symbol, int lookbackDays = 730, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return await base.GetHistoricalEarningsAsync(symbol, lookbackDays, cancellationToken);
    }
}

internal class BridgeTestRiskFreeRateProvider : DTpr005A
{
    public virtual Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0.0525m);

    public virtual Task<IReadOnlyDictionary<DateTime, decimal>> GetHistoricalRatesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<DateTime, decimal> rates = new Dictionary<DateTime, decimal>
        {
            { DateTime.UtcNow.Date.AddDays(-7), 0.0520m },
            { DateTime.UtcNow.Date, 0.0525m }
        };
        return Task.FromResult(rates);
    }
}

internal class BridgeTestRiskFreeRateProviderWithDelay : BridgeTestRiskFreeRateProvider
{
    private readonly TimeSpan _delay;

    public BridgeTestRiskFreeRateProviderWithDelay(TimeSpan delay) => _delay = delay;

    public override async Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return 0.0525m;
    }
}

internal class BridgeTestPassingValidator : DTqc002A
{
    public string ComponentId => "BridgeTestPassingValidator";

    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            DataElement = "Snapshot",
            Status = ValidationStatus.Passed,
            Message = "Mock validation passed"
        };
    }
}

internal class BridgeTestFailingValidator : DTqc002A
{
    public string ComponentId => "BridgeTestFailingValidator";

    public DataQualityResult Validate(MarketDataSnapshot snapshot)
    {
        return new DataQualityResult
        {
            ValidatorId = ComponentId,
            DataElement = "Snapshot",
            Status = ValidationStatus.Failed,
            Message = "Mock validation failed - test failure"
        };
    }
}
