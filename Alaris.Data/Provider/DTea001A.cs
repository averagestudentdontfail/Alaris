using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Alaris.Data.Model;

namespace Alaris.Data.Provider.FMP;

/// <summary>
/// Financial Modeling Prep earnings calendar provider.
/// Component ID: DTea001A
/// </summary>
/// <remarks>
/// Implements DTpr004A (Earnings Calendar Provider interface) using FMP free tier (250 calls/day).
/// 
/// API Documentation: https://financialmodelingprep.com/developer/docs/#Earnings-Calendar
/// Endpoint: /v3/earnings-calendar
/// 
/// Free tier provides:
/// - Earnings dates (confirmed and estimated)
/// - EPS estimates and actuals
/// - Fiscal quarter information
/// - Up to 250 API calls per day
/// </remarks>
public sealed class FinancialModelingPrepProvider : DTpr004A
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FinancialModelingPrepProvider> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://financialmodelingprep.com/api";

    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialModelingPrepProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="configuration">Configuration containing API key.</param>
    /// <param name="logger">Logger instance.</param>
    public FinancialModelingPrepProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FinancialModelingPrepProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _apiKey = configuration["FMP:ApiKey"]
            ?? throw new InvalidOperationException("FMP API key not configured");

        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EarningsEvent>> GetUpcomingEarningsAsync(
        string symbol,
        int daysAhead = 90,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation(
            "Fetching upcoming earnings for {Symbol} ({Days} days ahead)",
            symbol, daysAhead);

        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(daysAhead);

        var allEarnings = await GetEarningsInDateRangeAsync(
            startDate,
            endDate,
            cancellationToken);

        var symbolEarnings = allEarnings
            .Where(e => string.Equals(e.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Date)
            .ToList();

        _logger.LogInformation(
            "Found {Count} upcoming earnings for {Symbol}",
            symbolEarnings.Count, symbol);

        return symbolEarnings;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EarningsEvent>> GetHistoricalEarningsAsync(
        string symbol,
        int lookbackDays = 730,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or whitespace", nameof(symbol));

        _logger.LogInformation(
            "Fetching historical earnings for {Symbol} ({Days} days back)",
            symbol, lookbackDays);

        // FMP historical earnings endpoint: /v3/historical/earning_calendar/{symbol}
        var url = $"/v3/historical/earning_calendar/{symbol}?apikey={_apiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<FmpEarningsEvent[]>(
                url,
                cancellationToken: cancellationToken);

            if (response == null || response.Length == 0)
            {
                _logger.LogWarning("No historical earnings found for {Symbol}", symbol);
                return Array.Empty<EarningsEvent>();
            }

            var cutoffDate = DateTime.UtcNow.Date.AddDays(-lookbackDays);

            var earnings = response
                .Where(e => e.Date >= cutoffDate)
                .Select(e => MapToEarningsEvent(e, symbol))
                .OrderByDescending(e => e.Date)
                .ToList();

            _logger.LogInformation(
                "Retrieved {Count} historical earnings for {Symbol}",
                earnings.Count, symbol);

            return earnings;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching historical earnings for {Symbol}", symbol);
            throw new InvalidOperationException($"Failed to fetch historical earnings for {symbol}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetSymbolsWithEarningsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching symbols with earnings from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        var earnings = await GetEarningsInDateRangeAsync(startDate, endDate, cancellationToken);

        var symbols = earnings
            .Select(e => e.Symbol)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        _logger.LogInformation("Found {Count} symbols with earnings", symbols.Count);
        return symbols;
    }

    /// <summary>
    /// Gets all earnings events in a date range.
    /// </summary>
    private async Task<IReadOnlyList<EarningsEvent>> GetEarningsInDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        // FMP earnings calendar endpoint: /v3/earnings-calendar
        var url = $"/v3/earnings-calendar?from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}&apikey={_apiKey}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<FmpEarningsEvent[]>(
                url,
                cancellationToken: cancellationToken);

            if (response == null || response.Length == 0)
            {
                _logger.LogWarning(
                    "No earnings events found from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    startDate, endDate);
                return Array.Empty<EarningsEvent>();
            }

            var earnings = response
                .Select(e => MapToEarningsEvent(e, e.Symbol))
                .ToList();

            return earnings;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error fetching earnings calendar from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            throw new InvalidOperationException("Failed to fetch earnings calendar", ex);
        }
    }

    /// <summary>
    /// Maps FMP API response to EarningsEvent model.
    /// </summary>
    private static EarningsEvent MapToEarningsEvent(FmpEarningsEvent fmpEvent, string symbol)
    {
        // Parse timing from FMP time field (e.g., "bmo" = before market open, "amc" = after market close)
        var timing = fmpEvent.Time?.ToLowerInvariant() switch
        {
            "bmo" => EarningsTiming.BeforeMarketOpen,
            "amc" => EarningsTiming.AfterMarketClose,
            "dmh" => EarningsTiming.DuringMarketHours,
            _ => EarningsTiming.Unknown
        };

        return new EarningsEvent
        {
            Symbol = symbol,
            Date = fmpEvent.Date,
            FiscalQuarter = fmpEvent.FiscalQuarter,
            FiscalYear = fmpEvent.FiscalYear,
            Timing = timing,
            EpsEstimate = fmpEvent.EpsEstimate,
            EpsActual = fmpEvent.Eps,
            Source = "FinancialModelingPrep",
            FetchedAt = DateTime.UtcNow
        };
    }
}

#region FMP API Response Models

internal sealed class FmpEarningsEvent
{
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("date")]
    public required DateTime Date { get; init; }

    [JsonPropertyName("fiscalQuarter")]
    public string? FiscalQuarter { get; init; }

    [JsonPropertyName("fiscalYear")]
    public int? FiscalYear { get; init; }

    [JsonPropertyName("time")]
    public string? Time { get; init; }

    [JsonPropertyName("epsEstimate")]
    public decimal? EpsEstimate { get; init; }

    [JsonPropertyName("eps")]
    public decimal? Eps { get; init; }

    [JsonPropertyName("revenueEstimate")]
    public decimal? RevenueEstimate { get; init; }

    [JsonPropertyName("revenue")]
    public decimal? Revenue { get; init; }
}

#endregion