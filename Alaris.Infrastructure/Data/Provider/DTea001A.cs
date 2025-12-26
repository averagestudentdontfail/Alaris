// DTea001A.cs - Financial Modeling Prep earnings calendar provider

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Data.Http.Contracts;

namespace Alaris.Infrastructure.Data.Provider.FMP;

/// <summary>
/// Financial Modeling Prep earnings calendar provider.
/// Component ID: DTea001A
/// </summary>
/// <remarks>
/// <para>
/// Implements DTpr004A (Earnings Calendar Provider interface) using FMP free tier (250 calls/day)
/// via Refit declarative interface (IFinancialModelingPrepApi).
/// </para>
/// <para>
/// API Documentation: https://financialmodelingprep.com/developer/docs/#Earnings-Calendar
/// Endpoint: /v3/earnings-calendar
/// </para>
/// <para>
/// Free tier provides:
/// - Earnings dates (confirmed and estimated)
/// - EPS estimates and actuals
/// - Fiscal quarter information
/// - Up to 250 API calls per day
/// </para>
/// <para>
/// Resilience provided by Microsoft.Extensions.Http.Resilience standard handler.
/// </para>
/// </remarks>
public sealed class FinancialModelingPrepProvider : DTpr004A
{
    private readonly IFinancialModelingPrepApi _api;
    private readonly ILogger<FinancialModelingPrepProvider> _logger;
    private readonly string _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialModelingPrepProvider"/> class.
    /// </summary>
    /// <param name="api">FMP Refit API client.</param>
    /// <param name="configuration">Configuration containing API key.</param>
    /// <param name="logger">Logger instance.</param>
    public FinancialModelingPrepProvider(
        IFinancialModelingPrepApi api,
        IConfiguration configuration,
        ILogger<FinancialModelingPrepProvider> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _apiKey = configuration["FMP:ApiKey"]
            ?? throw new InvalidOperationException("FMP API key not configured");
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

        DateTime startDate = DateTime.UtcNow.Date;
        DateTime endDate = startDate.AddDays(daysAhead);

        IReadOnlyList<EarningsEvent> allEarnings = await GetEarningsInDateRangeAsync(
            startDate,
            endDate,
            cancellationToken);

        List<EarningsEvent> symbolEarnings = allEarnings
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

        try
        {
            FmpEarningsEvent[] response = await _api.GetHistoricalEarningsAsync(
                symbol,
                _apiKey,
                cancellationToken);

            if (response == null || response.Length == 0)
            {
                _logger.LogWarning("No historical earnings found for {Symbol}", symbol);
                return Array.Empty<EarningsEvent>();
            }

            DateTime cutoffDate = DateTime.UtcNow.Date.AddDays(-lookbackDays);

            List<EarningsEvent> earnings = response
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

        IReadOnlyList<EarningsEvent> earnings = await GetEarningsInDateRangeAsync(startDate, endDate, cancellationToken);

        List<string> symbols = earnings
            .Select(e => e.Symbol)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        _logger.LogInformation("Found {Count} symbols with earnings", symbols.Count);
        return symbols;
    }

    /// <inheritdoc/>
    public void EnableCacheOnlyMode()
    {
        // FMP is a paid API provider, cache-only mode not applicable
        _logger.LogDebug("EnableCacheOnlyMode called on FMP provider (no-op)");
    }

    /// <summary>
    /// Gets all earnings events in a date range using Refit interface.
    /// </summary>
    private async Task<IReadOnlyList<EarningsEvent>> GetEarningsInDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            FmpEarningsEvent[] response = await _api.GetEarningsCalendarAsync(
                startDate.ToString("yyyy-MM-dd"),
                endDate.ToString("yyyy-MM-dd"),
                _apiKey,
                cancellationToken);

            if (response == null || response.Length == 0)
            {
                _logger.LogWarning(
                    "No earnings events found from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                    startDate, endDate);
                return Array.Empty<EarningsEvent>();
            }

            List<EarningsEvent> earnings = response
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
        EarningsTiming timing = fmpEvent.Time?.ToLowerInvariant() switch
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
