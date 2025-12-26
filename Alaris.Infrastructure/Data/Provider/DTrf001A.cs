// DTrf001A.cs - Treasury Direct API risk-free rate provider

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Infrastructure.Data.Http.Contracts;

namespace Alaris.Infrastructure.Data.Provider.Treasury;

/// <summary>
/// Treasury Direct API risk-free rate provider.
/// Component ID: DTrf001A
/// </summary>
/// <remarks>
/// <para>
/// Provides risk-free rates using official US Treasury data
/// via Refit declarative interface (ITreasuryDirectApi).
/// Implements DTpr005A (Risk-Free Rate Provider interface).
/// </para>
/// <para>
/// API: https://www.treasurydirect.gov/TA_WS/securities
/// Cost: FREE (official US government API)
/// Data: Daily treasury yields (3-month T-bill recommended for options)
/// No authentication required
/// </para>
/// <para>
/// Usage:
/// - 3-month T-bill rate used as risk-free rate (r parameter)
/// - Updated daily
/// - No rate limits on official API
/// </para>
/// <para>
/// Resilience provided by Microsoft.Extensions.Http.Resilience standard handler.
/// </para>
/// </remarks>
public sealed class TreasuryDirectRateProvider : DTpr005A
{
    private readonly ITreasuryDirectApi _api;
    private readonly ILogger<TreasuryDirectRateProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TreasuryDirectRateProvider"/> class.
    /// </summary>
    /// <param name="api">Treasury Direct Refit API client.</param>
    /// <param name="logger">Logger instance.</param>
    public TreasuryDirectRateProvider(
        ITreasuryDirectApi api,
        ILogger<TreasuryDirectRateProvider> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<decimal> GetCurrentRateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching current 3-month T-bill rate");

        try
        {
            DateTime today = DateTime.UtcNow.Date;
            DateTime startDate = today.AddDays(-7);

            TreasurySecurityDto[] securities = await _api.SearchSecuritiesAsync(
                securityType: "Bill",
                dateFieldName: "issueDate",
                startDate: startDate.ToString("yyyy-MM-dd"),
                endDate: today.ToString("yyyy-MM-dd"),
                format: "json",
                cancellationToken);

            if (securities == null || securities.Length == 0)
            {
                _logger.LogError("No recent T-bill auctions found. Cannot determine risk-free rate.");
                throw new InvalidOperationException(
                    "No T-bill auction data found. Treasury Direct API may be unavailable.");
            }

            // Find most recent 3-month T-bill (91-day maturity)
            TreasurySecurityDto? threeMonthBill = securities
                .Where(s => s.Term != null && s.Term.Contains("91"))
                .OrderByDescending(s => s.IssueDate)
                .FirstOrDefault();

            if (threeMonthBill == null)
            {
                _logger.LogWarning("No 3-month T-bills found in recent auctions");
                
                // Use any bill as fallback
                TreasurySecurityDto anyBill = securities
                    .OrderByDescending(s => s.IssueDate)
                    .First();
                
                decimal fallbackRate = ParseRate(anyBill.InterestRate);
                _logger.LogInformation("Using fallback rate from {Term}: {Rate:P4}", anyBill.Term, fallbackRate);
                return fallbackRate;
            }

            decimal rate = ParseRate(threeMonthBill.InterestRate);
            
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
            TreasurySecurityDto[] securities = await _api.SearchSecuritiesAsync(
                securityType: "Bill",
                dateFieldName: "issueDate",
                startDate: startDate.ToString("yyyy-MM-dd"),
                endDate: endDate.ToString("yyyy-MM-dd"),
                format: "json",
                cancellationToken);

            if (securities == null || securities.Length == 0)
            {
                _logger.LogError("Treasury API returned no data for range {Start} to {End}. Check Date Range or API Status.", startDate, endDate);
                throw new InvalidOperationException($"No Treasury data found for {startDate:d}-{endDate:d}");
            }

            // Group by issue date, prefer 3-month bills
            Dictionary<DateTime, decimal> ratesByDate = securities
                .Where(s => !string.IsNullOrWhiteSpace(s.InterestRate))
                .GroupBy(s => s.IssueDate.Date)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // Prefer 3-month bill if available (Term usually '13-Week' or similar, strict check '91')
                        TreasurySecurityDto preferred = g.FirstOrDefault(s => s.Term != null && s.Term.Contains("91"))
                            ?? g.First();
                        return ParseRate(preferred.InterestRate!);
                    });

            _logger.LogInformation("Retrieved {Count} historical rate observations from Treasury Direct", ratesByDate.Count);

            return ratesByDate;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching historical T-bill rates");
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

        string cleaned = new(rateString.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

        if (!decimal.TryParse(cleaned, out decimal rate))
        {
            return 0m;
        }

        return rate / 100m;
    }
}
