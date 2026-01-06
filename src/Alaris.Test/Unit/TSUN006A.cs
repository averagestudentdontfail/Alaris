// DataValidatorTests.cs - Unit Tests for Data Quality Validators
// Tests: DTqc001A (PriceReasonableness), DTqc002A (IvArbitrage),
//        DTqc003A (VolumeOI), DTqc004A (EarningsDate)

using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Alaris.Core.Time;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Quality;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for PriceReasonablenessValidator (DTqc001A).
/// Validates price data quality rules.
/// </summary>
public sealed class DTqc001ATests : IDisposable
{
    private readonly PriceReasonablenessValidator _validator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PriceReasonablenessValidator> _logger;
    private readonly ITimeProvider _timeProvider;
    private bool _disposed;

    public DTqc001ATests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<PriceReasonablenessValidator>();
        _timeProvider = new LiveTimeProvider();
        _validator = new PriceReasonablenessValidator(_logger, _timeProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _loggerFactory.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public void ComponentId_ShouldBe_DTqc001A()
    {
        _validator.ComponentId.Should().Be("DTqc001A");
    }

    [Fact]
    public void Validate_ValidSnapshot_ShouldPass()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateValidSnapshot();

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Passed);
    }

    [Fact]
    public void Validate_SpotPriceChanged_MoreThan10Percent_ShouldWarn()
    {
        // Arrange - 20% change from previous close of 100
        // Use OTM call (strike 150) to avoid triggering ask-below-intrinsic check
        MarketDataSnapshot snapshot = CreateValidSnapshot(
            spotPrice: 120m,
            optionStrike: 150m,  // OTM strike
            optionBid: 1.00m,
            optionAsk: 1.20m);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("changed"));
    }

    [Fact]
    public void Validate_ZeroBid_ShouldFail()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateValidSnapshot(optionBid: 0m);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Message.Should().Contain("Invalid bid");
    }

    [Fact]
    public void Validate_BidGreaterThanAsk_ShouldFail()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateValidSnapshot(optionBid: 5.00m, optionAsk: 4.00m);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Message.Should().Contain("bid/ask spread");
    }

    [Fact]
    public void Validate_StaleData_ShouldFail()
    {
        // Arrange - 2 hours old data
        MarketDataSnapshot snapshot = CreateValidSnapshot(timestamp: DateTime.UtcNow.AddHours(-2));

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Message.Should().Contain("Stale data");
    }

    [Fact]
    public void Validate_AskBelowIntrinsic_ShouldFail()
    {
        // Arrange - Call with strike 100, spot 110, intrinsic = 10, ask = 5 (below intrinsic)
        MarketDataSnapshot snapshot = CreateValidSnapshot(
            spotPrice: 110m,
            optionStrike: 100m,
            optionRight: OptionRight.Call,
            optionAsk: 5m);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Message.Should().Contain("Ask below intrinsic");
    }

    private static MarketDataSnapshot CreateValidSnapshot(
        decimal spotPrice = 100m,
        DateTime? timestamp = null,
        decimal optionBid = 2.50m,
        decimal optionAsk = 2.70m,
        decimal optionStrike = 100m,
        OptionRight optionRight = OptionRight.Call)
    {
        return new MarketDataSnapshot
        {
            Symbol = "AAPL",
            Timestamp = timestamp ?? DateTime.UtcNow,
            SpotPrice = spotPrice,
            RiskFreeRate = 0.05m,
            DividendYield = 0.01m,
            AverageVolume30Day = 5_000_000m,
            HistoricalBars = new List<PriceBar>
            {
                new PriceBar { Symbol = "AAPL", Timestamp = DateTime.UtcNow.AddDays(-1), Open = 99, High = 101, Low = 98, Close = 100, Volume = 1000000 }
            },
            OptionChain = new OptionChainSnapshot
            {
                Symbol = "AAPL",
                SpotPrice = spotPrice,
                Timestamp = timestamp ?? DateTime.UtcNow,
                Contracts = new List<OptionContract>
                {
                    new OptionContract
                    {
                        UnderlyingSymbol = "AAPL",
                        OptionSymbol = "AAPL250117C00100000",
                        Strike = optionStrike,
                        Expiration = DateTime.UtcNow.AddDays(30),
                        Right = optionRight,
                        Bid = optionBid,
                        Ask = optionAsk,
                        Last = (optionBid + optionAsk) / 2,
                        Volume = 500,
                        OpenInterest = 1000,
                        ImpliedVolatility = 0.25m,
                        Timestamp = DateTime.UtcNow
                    }
                }
            },
            HistoricalEarnings = new List<EarningsEvent>()
        };
    }
}

/// <summary>
/// Unit tests for IvArbitrageValidator (DTqc002A interface test).
/// </summary>
public sealed class DTqc002AValidatorTests : IDisposable
{
    private readonly IvArbitrageValidator _validator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<IvArbitrageValidator> _logger;
    private bool _disposed;

    public DTqc002AValidatorTests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<IvArbitrageValidator>();
        _validator = new IvArbitrageValidator(_logger);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _loggerFactory.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public void ComponentId_ShouldBe_DTqc002A()
    {
        _validator.ComponentId.Should().Be("DTqc002A");
    }

    [Fact]
    public void Validate_ValidSnapshot_ShouldPass()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateSnapshotWithPutCallParity();

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().BeOneOf(ValidationStatus.Passed, ValidationStatus.PassedWithWarnings);
    }

    [Fact]
    public void Validate_PutCallParityViolation_ShouldWarn()
    {
        // Arrange - Violate parity: Call much higher relative to put
        MarketDataSnapshot snapshot = CreateSnapshotWithPutCallParity(
            callBid: 10m, callAsk: 11m,
            putBid: 0.5m, putAsk: 0.6m);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("parity"));
    }

    private static MarketDataSnapshot CreateSnapshotWithPutCallParity(
        decimal callBid = 5.00m, decimal callAsk = 5.20m,
        decimal putBid = 4.80m, decimal putAsk = 5.00m)
    {
        DateTime expiration = DateTime.UtcNow.AddDays(30);
        return new MarketDataSnapshot
        {
            Symbol = "AAPL",
            Timestamp = DateTime.UtcNow,
            SpotPrice = 100m,
            RiskFreeRate = 0.05m,
            DividendYield = 0.01m,
            AverageVolume30Day = 5_000_000m,
            HistoricalBars = new List<PriceBar>(),
            OptionChain = new OptionChainSnapshot
            {
                Symbol = "AAPL",
                SpotPrice = 100m,
                Timestamp = DateTime.UtcNow,
                Contracts = new List<OptionContract>
                {
                    new OptionContract
                    {
                        UnderlyingSymbol = "AAPL",
                        OptionSymbol = "AAPL250117C00100000",
                        Strike = 100m,
                        Expiration = expiration,
                        Right = OptionRight.Call,
                        Bid = callBid,
                        Ask = callAsk,
                        ImpliedVolatility = 0.25m,
                        Timestamp = DateTime.UtcNow
                    },
                    new OptionContract
                    {
                        UnderlyingSymbol = "AAPL",
                        OptionSymbol = "AAPL250117P00100000",
                        Strike = 100m,
                        Expiration = expiration,
                        Right = OptionRight.Put,
                        Bid = putBid,
                        Ask = putAsk,
                        ImpliedVolatility = 0.25m,
                        Timestamp = DateTime.UtcNow
                    }
                }
            },
            HistoricalEarnings = new List<EarningsEvent>()
        };
    }
}

/// <summary>
/// Unit tests for VolumeOpenInterestValidator (DTqc003A).
/// Validates volume and liquidity rules.
/// </summary>
public sealed class DTqc003ATests : IDisposable
{
    private readonly VolumeOpenInterestValidator _validator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<VolumeOpenInterestValidator> _logger;
    private bool _disposed;

    public DTqc003ATests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<VolumeOpenInterestValidator>();
        _validator = new VolumeOpenInterestValidator(_logger);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _loggerFactory.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public void ComponentId_ShouldBe_DTqc003A()
    {
        _validator.ComponentId.Should().Be("DTqc003A");
    }

    [Fact]
    public void Validate_ValidLiquidOption_ShouldPass()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateLiquidSnapshot();

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Passed);
    }

    [Fact]
    public void Validate_ZeroAverageVolume_ShouldFail()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateLiquidSnapshot(averageVolume: 0m);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Message.Should().Contain("zero");
    }

    [Fact]
    public void Validate_LiquidOptionWithNoVolume_ShouldWarn()
    {
        // Arrange - Liquid (high OI) but no volume
        MarketDataSnapshot snapshot = CreateLiquidSnapshot(optionOpenInterest: 500, optionVolume: 0);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("No volume"));
    }

    [Fact]
    public void Validate_HighVolumeToOiRatio_ShouldWarn()
    {
        // Arrange - Volume 5x OI
        MarketDataSnapshot snapshot = CreateLiquidSnapshot(optionOpenInterest: 100, optionVolume: 500);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("volume/OI"));
    }

    [Fact]
    public void Validate_VolumeSpike_ShouldWarn()
    {
        // Arrange - 15x average volume
        MarketDataSnapshot snapshot = CreateLiquidSnapshot(
            averageVolume: 1_000_000m,
            underlyingVolume: 15_000_000);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("spike") || w.Contains("Unusual"));
    }

    private static MarketDataSnapshot CreateLiquidSnapshot(
        decimal averageVolume = 5_000_000m,
        long underlyingVolume = 5_000_000,
        long optionOpenInterest = 1000,
        long optionVolume = 500)
    {
        return new MarketDataSnapshot
        {
            Symbol = "AAPL",
            Timestamp = DateTime.UtcNow,
            SpotPrice = 100m,
            RiskFreeRate = 0.05m,
            DividendYield = 0.01m,
            AverageVolume30Day = averageVolume,
            HistoricalBars = new List<PriceBar>
            {
                new PriceBar { Symbol = "AAPL", Timestamp = DateTime.UtcNow.AddDays(-1), Open = 99, High = 101, Low = 98, Close = 100, Volume = underlyingVolume }
            },
            OptionChain = new OptionChainSnapshot
            {
                Symbol = "AAPL",
                SpotPrice = 100m,
                Timestamp = DateTime.UtcNow,
                Contracts = new List<OptionContract>
                {
                    new OptionContract
                    {
                        UnderlyingSymbol = "AAPL",
                        OptionSymbol = "AAPL250117C00100000",
                        Strike = 100m,
                        Expiration = DateTime.UtcNow.AddDays(30),
                        Right = OptionRight.Call,
                        Bid = 2.50m,
                        Ask = 2.70m,
                        Volume = optionVolume,
                        OpenInterest = optionOpenInterest,
                        Timestamp = DateTime.UtcNow
                    }
                }
            },
            HistoricalEarnings = new List<EarningsEvent>()
        };
    }
}

/// <summary>
/// Unit tests for EarningsDateValidator (DTqc004A).
/// Validates earnings date accuracy rules.
/// </summary>
public sealed class DTqc004ATests : IDisposable
{
    private readonly EarningsDateValidator _validator;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<EarningsDateValidator> _logger;
    private readonly ITimeProvider _timeProvider;
    private bool _disposed;

    public DTqc004ATests()
    {
        _loggerFactory = new LoggerFactory();
        _logger = _loggerFactory.CreateLogger<EarningsDateValidator>();
        _timeProvider = new LiveTimeProvider();
        _validator = new EarningsDateValidator(_logger, _timeProvider);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _loggerFactory.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public void ComponentId_ShouldBe_DTqc004A()
    {
        _validator.ComponentId.Should().Be("DTqc004A");
    }

    [Fact]
    public void Validate_NoUpcomingEarnings_ShouldPass()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateSnapshotWithoutEarnings();

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Passed);
        result.Message.Should().Contain("No upcoming earnings");
    }

    [Fact]
    public void Validate_ValidEarningsDate_ShouldPass()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateSnapshotWithEarnings(daysAhead: 30);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().BeOneOf(ValidationStatus.Passed, ValidationStatus.PassedWithWarnings);
    }

    [Fact]
    public void Validate_EarningsDateInPast_ShouldFail()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateSnapshotWithEarnings(daysAhead: -5);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.Failed);
        result.Message.Should().Contain("in the past");
    }

    [Fact]
    public void Validate_EarningsMoreThan90DaysAhead_ShouldWarn()
    {
        // Arrange
        MarketDataSnapshot snapshot = CreateSnapshotWithEarnings(daysAhead: 120);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("90") || w.Contains("days"));
    }

    [Fact]
    public void Validate_StaleEarningsData_ShouldWarn()
    {
        // Arrange - 10 days old earnings data
        MarketDataSnapshot snapshot = CreateSnapshotWithEarnings(daysAhead: 30, fetchedDaysAgo: 10);

        // Act
        DataQualityResult result = _validator.Validate(snapshot);

        // Assert
        result.Status.Should().Be(ValidationStatus.PassedWithWarnings);
        result.Warnings.Should().Contain(w => w.Contains("days old"));
    }

    private static MarketDataSnapshot CreateSnapshotWithoutEarnings()
    {
        return new MarketDataSnapshot
        {
            Symbol = "AAPL",
            Timestamp = DateTime.UtcNow,
            SpotPrice = 100m,
            RiskFreeRate = 0.05m,
            DividendYield = 0.01m,
            AverageVolume30Day = 5_000_000m,
            HistoricalBars = new List<PriceBar>(),
            OptionChain = new OptionChainSnapshot
            {
                Symbol = "AAPL",
                SpotPrice = 100m,
                Timestamp = DateTime.UtcNow,
                Contracts = new List<OptionContract>()
            },
            NextEarnings = null,
            HistoricalEarnings = new List<EarningsEvent>()
        };
    }

    private static MarketDataSnapshot CreateSnapshotWithEarnings(int daysAhead, int fetchedDaysAgo = 0)
    {
        return new MarketDataSnapshot
        {
            Symbol = "AAPL",
            Timestamp = DateTime.UtcNow,
            SpotPrice = 100m,
            RiskFreeRate = 0.05m,
            DividendYield = 0.01m,
            AverageVolume30Day = 5_000_000m,
            HistoricalBars = new List<PriceBar>(),
            OptionChain = new OptionChainSnapshot
            {
                Symbol = "AAPL",
                SpotPrice = 100m,
                Timestamp = DateTime.UtcNow,
                Contracts = new List<OptionContract>()
            },
            NextEarnings = new EarningsEvent
            {
                Symbol = "AAPL",
                Date = DateTime.UtcNow.Date.AddDays(daysAhead),
                FiscalQuarter = "Q1",
                FiscalYear = 2025,
                Timing = EarningsTiming.AfterMarketClose,
                Source = "EarningsCalendar",
                FetchedAt = DateTime.UtcNow.AddDays(-fetchedDaysAgo)
            },
            HistoricalEarnings = new List<EarningsEvent>
            {
                new EarningsEvent
                {
                    Symbol = "AAPL",
                    Date = DateTime.UtcNow.Date.AddDays(-90),
                    FiscalQuarter = "Q4",
                    FiscalYear = 2024,
                    Timing = EarningsTiming.AfterMarketClose,
                    Source = "EarningsCalendar",
                    FetchedAt = DateTime.UtcNow.AddDays(-90)
                }
            }
        };
    }
}
