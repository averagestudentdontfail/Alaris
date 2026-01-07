// DTAP003A.cs - Treasury Direct API contract

using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Alaris.Infrastructure.Data.Http.Contracts;

/// <summary>
/// Treasury Direct REST API contract.
/// Component ID: DTAP003A
/// </summary>
/// <remarks>
/// <para>
/// API Documentation: https://www.treasurydirect.gov/TA_WS/documentation.htm
/// Base URL: https://www.treasurydirect.gov/TA_WS/securities
/// Authentication: None required
/// </para>
/// <para>
/// Uses 3-month T-bill rate as risk-free rate for option pricing.
/// </para>
/// </remarks>
public interface ITreasuryDirectApi
{
    /// <summary>
    /// Searches for treasury securities by type and date range.
    /// </summary>
    [Get("/search")]
    Task<TreasurySecurityDto[]> SearchSecuritiesAsync(
        [AliasAs("type")] string securityType,
        [AliasAs("dateFieldName")] string dateFieldName,
        [AliasAs("startDate")] string startDate,
        [AliasAs("endDate")] string endDate,
        [AliasAs("format")] string format,
        CancellationToken cancellationToken = default);
}

#region Response DTOs

public sealed class TreasurySecurityDto
{
    [JsonPropertyName("cusip")]
    public string? Cusip { get; init; }

    [JsonPropertyName("issueDate")]
    public DateTime IssueDate { get; init; }

    [JsonPropertyName("maturityDate")]
    public DateTime MaturityDate { get; init; }

    [JsonPropertyName("interestRate")]
    public string? InterestRate { get; init; }

    /// <summary>
    /// High discount rate for T-bills (discount instruments).
    /// This is the rate to use for T-bills since they don't have interest rates.
    /// </summary>
    [JsonPropertyName("highDiscountRate")]
    public string? HighDiscountRate { get; init; }

    /// <summary>
    /// High investment rate (equivalent yield) for T-bills.
    /// </summary>
    [JsonPropertyName("highInvestmentRate")]
    public string? HighInvestmentRate { get; init; }

    [JsonPropertyName("term")]
    public string? Term { get; init; }

    [JsonPropertyName("type")]
    public string? SecurityType { get; init; }
}

#endregion
