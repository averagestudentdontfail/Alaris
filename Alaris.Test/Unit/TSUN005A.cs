// TSUN005A.cs - Unit Tests for Data Providers (Mocked Refit Interfaces)
// Tests: DTpr001A (Polygon), DTea001A (FMP), DTrf001A (Treasury)

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Alaris.Infrastructure.Data.Provider.Polygon;
using Alaris.Infrastructure.Data.Provider.FMP;
using Alaris.Infrastructure.Data.Provider.Treasury;
using Alaris.Infrastructure.Data.Http.Contracts;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for PolygonApiClient (DTpr001A).
/// Uses mocked Refit interface for deterministic testing.
/// </summary>
public sealed class DTpr001ATests
{
    private readonly IPolygonApi _mockApi;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PolygonApiClient> _logger;

    public DTpr001ATests()
    {
        _mockApi = Substitute.For<IPolygonApi>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Polygon:ApiKey", "test_api_key" }
            })
            .Build();
        _logger = new LoggerFactory().CreateLogger<PolygonApiClient>();
    }

    [Fact]
    public async Task GetHistoricalBarsAsync_ValidResponse_ReturnsBars()
    {
        // Arrange
        PolygonAggregatesResponse response = new()
        {
            Results = new[]
            {
                new PolygonBar { Timestamp = 1704067200000L, Open = 100.0m, High = 102.0m, Low = 99.0m, Close = 101.0m, Volume = 1000000 }
            }
        };
        _mockApi.GetDailyBarsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        PolygonApiClient client = new(_mockApi, _configuration, _logger);

        // Act
        IReadOnlyList<Alaris.Infrastructure.Data.Model.PriceBar> bars = await client.GetHistoricalBarsAsync("AAPL", DateTime.Today.AddDays(-7), DateTime.Today);

        // Assert
        bars.Should().HaveCount(1);
        bars[0].Symbol.Should().Be("AAPL");
        bars[0].Close.Should().Be(101.0m);
    }

    [Fact]
    public async Task GetHistoricalBarsAsync_EmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        PolygonAggregatesResponse response = new() { Results = Array.Empty<PolygonBar>() };
        _mockApi.GetDailyBarsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        PolygonApiClient client = new(_mockApi, _configuration, _logger);

        // Act
        IReadOnlyList<Alaris.Infrastructure.Data.Model.PriceBar> bars = await client.GetHistoricalBarsAsync("INVALID", DateTime.Today.AddDays(-7), DateTime.Today);

        // Assert
        bars.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSpotPriceAsync_ValidResponse_ReturnsPrice()
    {
        // Arrange
        PolygonAggregatesResponse response = new()
        {
            Results = new[] { new PolygonBar { Timestamp = 1704067200000L, Open = 100.0m, High = 102.0m, Low = 99.0m, Close = 150.50m, Volume = 1000000 } }
        };
        _mockApi.GetPreviousDayAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        PolygonApiClient client = new(_mockApi, _configuration, _logger);

        // Act
        decimal price = await client.GetSpotPriceAsync("AAPL");

        // Assert
        price.Should().Be(150.50m);
    }

    [Fact]
    public async Task GetAverageVolume30DayAsync_CalculatesAverage()
    {
        // Arrange - 3 bars to average
        PolygonAggregatesResponse response = new()
        {
            Results = new[]
            {
                new PolygonBar { Timestamp = 1704067200000L, Open = 100m, High = 102m, Low = 99m, Close = 101m, Volume = 1000000 },
                new PolygonBar { Timestamp = 1704153600000L, Open = 101m, High = 103m, Low = 100m, Close = 102m, Volume = 2000000 },
                new PolygonBar { Timestamp = 1704240000000L, Open = 102m, High = 104m, Low = 101m, Close = 103m, Volume = 3000000 }
            }
        };
        _mockApi.GetDailyBarsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        PolygonApiClient client = new(_mockApi, _configuration, _logger);

        // Act
        decimal avgVolume = await client.GetAverageVolume30DayAsync("AAPL");

        // Assert
        avgVolume.Should().Be(2000000m); // (1M + 2M + 3M) / 3
    }

    [Fact]
    public void Constructor_MissingApiKey_ThrowsException()
    {
        // Arrange
        IConfiguration emptyConfig = new ConfigurationBuilder().Build();

        // Act & Assert
        Action act = () => _ = new PolygonApiClient(_mockApi, emptyConfig, _logger);
        act.Should().Throw<InvalidOperationException>().WithMessage("*API key*");
    }
}

/// <summary>
/// Unit tests for FinancialModelingPrepProvider (DTea001A).
/// Uses mocked Refit interface for deterministic testing.
/// </summary>
public sealed class DTea001ATests
{
    private readonly IFinancialModelingPrepApi _mockApi;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FinancialModelingPrepProvider> _logger;

    public DTea001ATests()
    {
        _mockApi = Substitute.For<IFinancialModelingPrepApi>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "FMP:ApiKey", "test_fmp_key" }
            })
            .Build();
        _logger = new LoggerFactory().CreateLogger<FinancialModelingPrepProvider>();
    }

    [Fact]
    public async Task GetHistoricalEarningsAsync_ValidResponse_ReturnsEarnings()
    {
        // Arrange
        FmpEarningsEvent[] response = new[]
        {
            new FmpEarningsEvent { Symbol = "AAPL", Date = DateTime.Today.AddDays(-30), FiscalQuarter = "Q4", FiscalYear = 2024, Time = "amc", Eps = 1.45m, EpsEstimate = 1.40m }
        };
        _mockApi.GetHistoricalEarningsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        FinancialModelingPrepProvider provider = new(_mockApi, _configuration, _logger);

        // Act
        IReadOnlyList<Alaris.Infrastructure.Data.Model.EarningsEvent> earnings = await provider.GetHistoricalEarningsAsync("AAPL", lookbackDays: 365);

        // Assert
        earnings.Should().HaveCount(1);
        earnings[0].Symbol.Should().Be("AAPL");
    }

    [Fact]
    public async Task GetSymbolsWithEarningsAsync_ReturnsDistinctSymbols()
    {
        // Arrange
        FmpEarningsEvent[] response = new[]
        {
            new FmpEarningsEvent { Symbol = "AAPL", Date = DateTime.Today.AddDays(5), Time = "amc" },
            new FmpEarningsEvent { Symbol = "MSFT", Date = DateTime.Today.AddDays(5), Time = "bmo" },
            new FmpEarningsEvent { Symbol = "AAPL", Date = DateTime.Today.AddDays(5), Time = "amc" } // Duplicate
        };
        _mockApi.GetEarningsCalendarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        FinancialModelingPrepProvider provider = new(_mockApi, _configuration, _logger);

        // Act
        IReadOnlyList<string> symbols = await provider.GetSymbolsWithEarningsAsync(DateTime.Today, DateTime.Today.AddDays(30));

        // Assert
        symbols.Should().HaveCount(2);
        symbols.Should().Contain("AAPL");
        symbols.Should().Contain("MSFT");
    }

    [Fact]
    public void Constructor_MissingApiKey_ThrowsException()
    {
        // Arrange
        IConfiguration emptyConfig = new ConfigurationBuilder().Build();

        // Act & Assert
        Action act = () => _ = new FinancialModelingPrepProvider(_mockApi, emptyConfig, _logger);
        act.Should().Throw<InvalidOperationException>().WithMessage("*API key*");
    }
}

/// <summary>
/// Unit tests for TreasuryDirectRateProvider (DTrf001A).
/// Uses mocked Refit interface for deterministic testing.
/// </summary>
public sealed class DTrf001ATests
{
    private readonly ITreasuryDirectApi _mockApi;
    private readonly ILogger<TreasuryDirectRateProvider> _logger;

    public DTrf001ATests()
    {
        _mockApi = Substitute.For<ITreasuryDirectApi>();
        _logger = new LoggerFactory().CreateLogger<TreasuryDirectRateProvider>();
    }

    [Fact]
    public async Task GetCurrentRateAsync_ValidResponse_ReturnsParsedRate()
    {
        // Arrange
        TreasurySecurityDto[] response = new[]
        {
            new TreasurySecurityDto { Cusip = "912796XY0", IssueDate = DateTime.Today.AddDays(-1), MaturityDate = DateTime.Today.AddDays(90), Term = "91 Day", InterestRate = "5.25", SecurityType = "Bill" }
        };
        _mockApi.SearchSecuritiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        TreasuryDirectRateProvider provider = new(_mockApi, _logger);

        // Act
        decimal rate = await provider.GetCurrentRateAsync();

        // Assert
        rate.Should().Be(0.0525m); // 5.25% converted to decimal
    }

    [Fact]
    public async Task GetCurrentRateAsync_NoData_ThrowsException()
    {
        // Arrange - Empty array response
        _mockApi.SearchSecuritiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<TreasurySecurityDto>()));

        TreasuryDirectRateProvider provider = new(_mockApi, _logger);

        // Act & Assert - Fail fast, no fallback data substitution
        Func<Task> act = () => provider.GetCurrentRateAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No T-bill auction data*");
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_ValidResponse_ReturnsRatesByDate()
    {
        // Arrange
        TreasurySecurityDto[] response = new[]
        {
            new TreasurySecurityDto { Cusip = "912796XY0", IssueDate = DateTime.Parse("2025-01-02", System.Globalization.CultureInfo.InvariantCulture), Term = "91 Day", InterestRate = "5.20" },
            new TreasurySecurityDto { Cusip = "912796XZ0", IssueDate = DateTime.Parse("2025-01-09", System.Globalization.CultureInfo.InvariantCulture), Term = "91 Day", InterestRate = "5.25" }
        };
        _mockApi.SearchSecuritiesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        TreasuryDirectRateProvider provider = new(_mockApi, _logger);

        // Act
        IReadOnlyDictionary<DateTime, decimal> rates = await provider.GetHistoricalRatesAsync(DateTime.Today.AddDays(-30), DateTime.Today);

        // Assert
        rates.Should().HaveCount(2);
        rates[DateTime.Parse("2025-01-02", System.Globalization.CultureInfo.InvariantCulture)].Should().Be(0.0520m);
        rates[DateTime.Parse("2025-01-09", System.Globalization.CultureInfo.InvariantCulture)].Should().Be(0.0525m);
    }
}
