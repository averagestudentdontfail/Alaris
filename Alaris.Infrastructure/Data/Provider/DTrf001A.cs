using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Provider;

namespace Alaris.Infrastructure.Data.Provider.Treasury;

/// <summary>
/// Treasury Direct API risk-free rate provider.
/// Component ID: DTrf001A
/// </summary>
/// <remarks>
/// Provides risk-free rates using official US Treasury data.
/// Implements DTpr005A (Risk-Free Rate Provider interface).
/// 
/// API: https://www.treasurydirect.gov/TA_WS/securities
/// Cost: FREE (official US government API)
/// Data: Daily treasury yields (3-month T-bill recommended for options)
/// No authentication required
/// 
/// Usage:
/// - 3-month T-bill rate used as risk-free rate (r parameter)
/// - Updated daily
/// - No rate limits on official API
/// </remarks>
public sealed class TreasuryDirectRateProvider : DTpr005A
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TreasuryDirectRateProvider> _logger;
    private const string BaseUrl = "https://www.treasurydirect.gov/TA_WS/securities/";

    /// <summary>
    /// Initializes a new instance of the <see cref="TreasuryDirectRateProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="logger">Logger instance.</param>
    public TreasuryDirectRateProvider(
        HttpClient httpClient,
        ILogger<TreasuryDirectRateProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Do not set BaseAddress on shared HttpClient
    }

    /// <inheritdoc/>
    public async Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching current 3-month T-bill rate");

        try
        {
            // Get today's date
            var today = DateTime.UtcNow.Date;
            
            // Treasury API endpoint: search?type=Bill&dateFieldName=issueDate&startDate=YYYY-MM-DD
            // Look back up to 7 days to handle weekends/holidays
            var startDate = today.AddDays(-7);
            var url = $"{BaseUrl}search?type=Bill&dateFieldName=issueDate&startDate={startDate:yyyy-MM-dd}&endDate={today:yyyy-MM-dd}&format=json";

            var securities = await _httpClient.GetFromJsonAsync<TreasurySecurity[]>(
                url,
                cancellationToken: cancellationToken);

            if (securities == null || securities.Length == 0)
            {
                _logger.LogError("No recent T-bill auctions found. Cannot determine risk-free rate.");
                throw new InvalidOperationException(
                    "No T-bill auction data found. Treasury Direct API may be unavailable.");
            }

            // Find most recent 3-month T-bill (91-day maturity)
            var threeMonthBill = securities
                .Where(s => s.Term != null && s.Term.Contains("91"))
                .OrderByDescending(s => s.IssueDate)
                .FirstOrDefault();

            if (threeMonthBill == null)
            {
                _logger.LogWarning("No 3-month T-bills found in recent auctions");
                
                // Use any bill as fallback
                var anyBill = securities
                    .OrderByDescending(s => s.IssueDate)
                    .First();
                
                var fallbackRate = ParseRate(anyBill.InterestRate);
                _logger.LogInformation("Using fallback rate from {Term}: {Rate:P4}", anyBill.Term, fallbackRate);
                return fallbackRate;
            }

            var rate = ParseRate(threeMonthBill.InterestRate);
            
            _logger.LogInformation(
                "Current 3-month T-bill rate: {Rate:P4} (issue date: {IssueDate:yyyy-MM-dd})",
                rate,
                threeMonthBill.IssueDate);

            return rate;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching T-bill rates. Cannot determine risk-free rate.");
            throw new InvalidOperationException(
                "Failed to fetch T-bill rates from Treasury Direct API.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error fetching T-bill rates.");
            throw new InvalidOperationException(
                "Failed to parse Treasury Direct API response.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<DateTime, decimal>> GetHistoricalRatesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching historical T-bill rates from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; AlarisTradingSystem/1.0)");
            }

            var url = $"{BaseUrl}search?type=Bill&dateFieldName=issueDate&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&format=json";

            var securities = await _httpClient.GetFromJsonAsync<TreasurySecurity[]>(
                url,
                cancellationToken: cancellationToken);

            if (securities == null || securities.Length == 0)
            {
                _logger.LogError("Treasury API returned no data for range {Start} to {End}. Check Date Range or API Status.", startDate, endDate);
                throw new InvalidOperationException($"No Treasury data found for {startDate:d}-{endDate:d}");
            }

            // Group by issue date, prefer 3-month bills
            var ratesByDate = securities
                .Where(s => !string.IsNullOrWhiteSpace(s.InterestRate))
                .GroupBy(s => s.IssueDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // Prefer 3-month bill if available (Term usually '13-Week' or similar, strict check '91')
                        var preferred = g.FirstOrDefault(s => s.Term != null && s.Term.Contains("91"))
                            ?? g.First();
                        return ParseRate(preferred.InterestRate!);
                    });

            _logger.LogInformation("Retrieved {Count} historical rate observations from Treasury Direct", ratesByDate.Count);

            return ratesByDate;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching historical T-bill rates from {Url}", $"{BaseUrl}search?...");
            throw; // Fail fast, do not use fake data
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error fetching historical T-bill rates");
            throw;
        }
    }

    /// <summary>
    /// Parses interest rate string to decimal.
    /// Treasury rates are typically given as percentages (e.g., "5.25" for 5.25%).
    /// </summary>
    private static decimal ParseRate(string? rateString)
    {
        if (string.IsNullOrWhiteSpace(rateString))
        {
            return 0m;
        }

        var cleaned = new string(rateString.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

        if (!decimal.TryParse(cleaned, out var rate))
        {
            return 0m;
        }

        return rate / 100m;
    }

    // Fallback rates removed - fail-fast principle
    // Historical data must come from Treasury Direct API, not fabricated constants.
}


file sealed class TreasurySecurity
{
    [JsonPropertyName("cusip")]
    public string? Cusip { get; init; }

    [JsonPropertyName("issueDate")]
    public DateTime IssueDate { get; init; }

    [JsonPropertyName("maturityDate")]
    public DateTime MaturityDate { get; init; }

    [JsonPropertyName("interestRate")]
    public string? InterestRate { get; init; }

    [JsonPropertyName("term")]
    public string? Term { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

