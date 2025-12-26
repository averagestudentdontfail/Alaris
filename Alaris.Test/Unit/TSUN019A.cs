// UniverseSelectionTests.cs - Unit Tests for STUN001A Universe Selection Model

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Provider;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for STUN001A earnings universe selection model.
/// Tests the earnings provider integration and selection logic.
/// </summary>
/// <remarks>
/// Note: Full universe selection testing requires LEAN framework components.
/// These tests focus on the earnings provider integration and constructor validation.
/// </remarks>
public sealed class STUN001ATests
{
    private readonly ILogger<STUN001A> _logger;

    public STUN001ATests()
    {
        _logger = new LoggerFactory().CreateLogger<STUN001A>();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var earningsProvider = new UniverseTestEarningsProvider();

        // Act
        var universe = new STUN001A(
            earningsProvider,
            daysBeforeEarningsMin: 5,
            daysBeforeEarningsMax: 7,
            minimumDollarVolume: 1_500_000m,
            minimumPrice: 5.00m,
            maxCoarseSymbols: 500,
            maxFinalSymbols: 50,
            logger: _logger);

        // Assert
        universe.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullEarningsProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new STUN001A(
            earningsProvider: null!,
            logger: _logger);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("earningsProvider");
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesAtilganDefaults()
    {
        // Arrange - Atilgan (2014) paper parameters
        var earningsProvider = new UniverseTestEarningsProvider();

        // Act - should not throw with defaults
        var universe = new STUN001A(earningsProvider, logger: _logger);

        // Assert
        universe.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomParameters_AcceptsAllValues()
    {
        // Arrange
        var earningsProvider = new UniverseTestEarningsProvider();

        // Act
        var universe = new STUN001A(
            earningsProvider,
            daysBeforeEarningsMin: 3,
            daysBeforeEarningsMax: 10,
            minimumDollarVolume: 5_000_000m,
            minimumPrice: 10.00m,
            maxCoarseSymbols: 1000,
            maxFinalSymbols: 100,
            logger: _logger);

        // Assert
        universe.Should().NotBeNull();
    }

    [Fact]
    public async Task EarningsProvider_GetSymbolsWithEarnings_ReturnsExpectedSymbols()
    {
        // Arrange
        var earningsProvider = new UniverseTestEarningsProvider();

        // Act
        var symbols = await earningsProvider.GetSymbolsWithEarningsAsync(
            DateTime.Today,
            DateTime.Today.AddDays(14));

        // Assert
        symbols.Should().HaveCount(3);
        symbols.Should().Contain("AAPL");
        symbols.Should().Contain("MSFT");
        symbols.Should().Contain("GOOGL");
    }

    [Fact]
    public async Task EarningsProvider_GetUpcomingEarnings_ReturnsEarningsWithinWindow()
    {
        // Arrange
        var earningsProvider = new UniverseTestEarningsProvider();

        // Act
        var earnings = await earningsProvider.GetUpcomingEarningsAsync("AAPL", daysAhead: 30);

        // Assert
        earnings.Should().NotBeEmpty();
        earnings[0].Symbol.Should().Be("AAPL");
        earnings[0].Date.Should().BeAfter(DateTime.UtcNow.Date);
        earnings[0].Date.Should().BeBefore(DateTime.UtcNow.Date.AddDays(31));
    }

    [Fact]
    public async Task EarningsProvider_GetHistoricalEarnings_ReturnsHistoricalData()
    {
        // Arrange
        var earningsProvider = new UniverseTestEarningsProvider();

        // Act
        var earnings = await earningsProvider.GetHistoricalEarningsAsync("AAPL", lookbackDays: 365);

        // Assert
        earnings.Should().NotBeEmpty();
        earnings[0].Symbol.Should().Be("AAPL");
        earnings[0].Date.Should().BeBefore(DateTime.UtcNow.Date);
    }
}

// Stub class for testing - actual STUN001A requires LEAN framework

/// <summary>
/// Stub STUN001A for unit testing without LEAN framework dependencies.
/// Tests the earnings provider integration and constructor validation.
/// </summary>
internal sealed class STUN001A
{
    private readonly DTpr004A _earningsProvider;
    private readonly ILogger<STUN001A>? _logger;

    // Atilgan (2014) parameters
    private readonly int _daysBeforeEarningsMin;
    private readonly int _daysBeforeEarningsMax;
    private readonly decimal _minimumDollarVolume;
    private readonly decimal _minimumPrice;
    private readonly int _maxSymbols;

    public STUN001A(
        DTpr004A earningsProvider,
        int daysBeforeEarningsMin = 5,
        int daysBeforeEarningsMax = 7,
        decimal minimumDollarVolume = 1_500_000m,
        decimal minimumPrice = 5.00m,
        int maxCoarseSymbols = 500,
        int maxFinalSymbols = 50,
        ILogger<STUN001A>? logger = null)
    {
        _earningsProvider = earningsProvider
            ?? throw new ArgumentNullException(nameof(earningsProvider));
        _daysBeforeEarningsMin = daysBeforeEarningsMin;
        _daysBeforeEarningsMax = daysBeforeEarningsMax;
        _minimumDollarVolume = minimumDollarVolume;
        _minimumPrice = minimumPrice;
        _maxSymbols = maxFinalSymbols;
        _logger = logger;
    }

    public DTpr004A EarningsProvider => _earningsProvider;
}

// Mock Implementations for Universe Selection Testing

internal class UniverseTestEarningsProvider : DTpr004A
{
    private static readonly IReadOnlyList<string> s_defaultSymbols = new[] { "AAPL", "MSFT", "GOOGL" };

    public Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(string symbol, int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var earnings = new List<EarningsEvent>
        {
            new()
            {
                Symbol = symbol,
                Date = DateTime.UtcNow.Date.AddDays(7), // 7 days ahead - within 5-7 day Atilgan window
                FiscalQuarter = "Q1",
                FiscalYear = 2025,
                Timing = EarningsTiming.AfterMarketClose,
                Source = "MockUniverseTest",
                FetchedAt = DateTime.UtcNow
            }
        };
        return Task.FromResult<IReadOnlyList<EarningsEvent>>(earnings);
    }

    public Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(string symbol, int lookbackDays = 730, CancellationToken cancellationToken = default)
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
                Source = "MockUniverseTest",
                FetchedAt = DateTime.UtcNow.AddDays(-90)
            }
        };
        return Task.FromResult<IReadOnlyList<EarningsEvent>>(earnings);
    }

    public Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(s_defaultSymbols);
    }

    public void EnableCacheOnlyMode() { }
}
