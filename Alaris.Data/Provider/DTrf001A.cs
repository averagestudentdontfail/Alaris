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
using Alaris.Data.Provider;

namespace Alaris.Data.Provider.Treasury;

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
                _logger.LogWarning("No recent T-bill auctions found, using fallback rate");
                return 0.0525m; // Fallback: 5.25% (typical 2025 rate)
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
            _logger.LogError(ex, "HTTP error fetching T-bill rates");
            _logger.LogWarning("Using fallback rate: 5.25%");
            return 0.0525m; // Fallback rate
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "JSON parse error fetching T-bill rates");
             return 0.0525m;
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
            var url = $"{BaseUrl}search?type=Bill&dateFieldName=issueDate&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&format=json";

            var securities = await _httpClient.GetFromJsonAsync<TreasurySecurity[]>(
                url,
                cancellationToken: cancellationToken);

            if (securities == null || securities.Length == 0)
            {
                _logger.LogWarning("No T-bill auctions found in date range");
                return new Dictionary<DateTime, decimal>();
            }

            // Group by issue date, prefer 3-month bills
            var ratesByDate = securities
                .Where(s => !string.IsNullOrWhiteSpace(s.InterestRate))
                .GroupBy(s => s.IssueDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // Prefer 3-month bill if available
                        var preferred = g.FirstOrDefault(s => s.Term != null && s.Term.Contains("91"))
                            ?? g.First();
                        return ParseRate(preferred.InterestRate!);
                    });

            _logger.LogInformation("Retrieved {Count} historical rate observations", ratesByDate.Count);

            return ratesByDate;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching historical T-bill rates");
            return new Dictionary<DateTime, decimal>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error fetching historical T-bill rates");
            return new Dictionary<DateTime, decimal>();
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
            // Should be filtered out upstream, but fail safe
            return 0m;
        }

        // Remove any non-numeric characters except decimal point and minus sign
        var cleaned = new string(rateString.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

        if (!decimal.TryParse(cleaned, out var rate))
        {
            // Log or throw? Return 0 to be safe
            return 0m;
        }

        // Convert from percentage to decimal (5.25 → 0.0525)
        return rate / 100m;
    }
}

#region Treasury Direct API Response Models

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

#endregion