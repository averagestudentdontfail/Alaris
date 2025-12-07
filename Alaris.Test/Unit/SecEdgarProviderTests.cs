// =============================================================================
// SecEdgarProviderTests.cs - Unit Tests for SEC EDGAR Provider
// Component: TSDTea001B | Category: Unit Test | Variant: A (Primary)
// =============================================================================
// Reference: Alaris.Governance/Structure.md § 4.4
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Alaris.Data.Provider.SEC;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for <see cref="SecEdgarProvider"/>.
/// </summary>
public sealed class SecEdgarProviderTests : IDisposable
{
    private readonly SecEdgarMockHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SecEdgarProvider> _logger;
    private readonly SecEdgarProvider _provider;

    public SecEdgarProviderTests()
    {
        _mockHandler = new SecEdgarMockHandler();
        _httpClient = new HttpClient(_mockHandler);
        _logger = new SecEdgarMockLogger<SecEdgarProvider>();
        _provider = new SecEdgarProvider(_httpClient, _logger);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _httpClient.Dispose();
        _mockHandler.Dispose();
        GC.SuppressFinalize(this);
    }

    // ========================================================================
    // Constructor Tests
    // ========================================================================

    [Fact]
    public void Constructor_ThrowsOnNullHttpClient()
    {
        // Act & Assert
        var act = () => _ = new SecEdgarProvider(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        // Act & Assert
        using var client = new HttpClient();
        var act = () => _ = new SecEdgarProvider(client, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_SetsUserAgentHeader()
    {
        // Assert
        _httpClient.DefaultRequestHeaders.UserAgent.ToString()
            .Should().Contain("Alaris");
    }

    // ========================================================================
    // GetHistoricalEarningsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetHistoricalEarningsAsync_WithValidSymbol_ReturnsEarnings()
    {
        // Arrange
        SetupCikMapping("AAPL", 320193);
        SetupCompanyFilings("0000320193", CreateValidFilingsResponse());

        // Act
        var result = await _provider.GetHistoricalEarningsAsync("AAPL");

        // Assert
        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(e =>
        {
            e.Symbol.Should().Be("AAPL");
            e.Source.Should().Be("SEC-EDGAR");
            e.Date.Should().BeBefore(DateTime.UtcNow);
        });
    }

    [Fact]
    public async Task GetHistoricalEarningsAsync_WithEmptySymbol_ThrowsArgumentException()
    {
        // Act & Assert
        Func<Task> act = () => _provider.GetHistoricalEarningsAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetHistoricalEarningsAsync_WithUnknownSymbol_ReturnsEmpty()
    {
        // Arrange
        SetupEmptyCikMapping();

        // Act
        var result = await _provider.GetHistoricalEarningsAsync("UNKNOWN");

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetUpcomingEarningsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetUpcomingEarningsAsync_WithEmptySymbol_ThrowsArgumentException()
    {
        // Act & Assert
        Func<Task> act = () => _provider.GetUpcomingEarningsAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ========================================================================
    // GetSymbolsWithEarningsAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetSymbolsWithEarningsAsync_ReturnsEmpty()
    {
        // SEC EDGAR doesn't support batch date range queries
        // Act
        var result = await _provider.GetSymbolsWithEarningsAsync(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow);

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetCikForTickerAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetCikForTickerAsync_WithValidTicker_ReturnsCik()
    {
        // Arrange
        SetupCikMapping("TSLA", 1318605);

        // Act
        var result = await _provider.GetCikForTickerAsync("TSLA");

        // Assert
        result.Should().Be("0001318605");
    }

    [Fact]
    public async Task GetCikForTickerAsync_WithLowercaseTicker_ReturnsCik()
    {
        // Arrange
        SetupCikMapping("GOOGL", 1652044);

        // Act
        var result = await _provider.GetCikForTickerAsync("googl");

        // Assert
        result.Should().Be("0001652044");
    }

    [Fact]
    public async Task GetCikForTickerAsync_WithEmptyTicker_ReturnsNull()
    {
        // Act
        var result = await _provider.GetCikForTickerAsync("");

        // Assert
        result.Should().BeNull();
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private void SetupCikMapping(string ticker, long cik)
    {
        var mapping = new Dictionary<string, object>
        {
            ["0"] = new { cik_str = cik, ticker = ticker, title = $"{ticker} Inc." }
        };

        _mockHandler.SetupResponse(
            "https://www.sec.gov/files/company_tickers.json",
            JsonSerializer.Serialize(mapping));
    }

    private void SetupEmptyCikMapping()
    {
        _mockHandler.SetupResponse(
            "https://www.sec.gov/files/company_tickers.json",
            "{}");
    }

    private void SetupCompanyFilings(string paddedCik, string response)
    {
        _mockHandler.SetupResponse(
            $"https://data.sec.gov/submissions/CIK{paddedCik}.json",
            response);
    }

    private static string CreateValidFilingsResponse()
    {
        var formTypes = new[] { "8-K", "8-K", "10-Q", "8-K" };
        var filingDates = new[]
        {
            DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime.UtcNow.AddDays(-90).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime.UtcNow.AddDays(-60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime.UtcNow.AddDays(-180).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        var primaryDocs = new[] { "doc1.htm", "doc2.htm", "doc3.htm", "doc4.htm" };
        var itemNumbers = new[] { "2.02", "2.02, 9.01", "N/A", "2.02" };

        return JsonSerializer.Serialize(new
        {
            cik = "320193",
            name = "Apple Inc.",
            filings = new
            {
                recent = new
                {
                    form = formTypes,
                    filingDate = filingDates,
                    primaryDocument = primaryDocs,
                    items = itemNumbers
                }
            }
        });
    }
}

/// <summary>
/// Mock HTTP message handler for SEC EDGAR testing.
/// </summary>
internal sealed class SecEdgarMockHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void SetupResponse(string url, string content)
    {
        _responses[url] = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;

        if (_responses.TryGetValue(url, out var content))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

/// <summary>
/// Mock logger for SEC EDGAR testing.
/// </summary>
internal sealed class SecEdgarMockLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // No-op for tests
    }
}
