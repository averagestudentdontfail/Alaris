// DTAP002A.cs - Financial Modeling Prep API contract

using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Alaris.Infrastructure.Data.Http.Contracts;

/// <summary>
/// Financial Modeling Prep REST API contract.
/// Component ID: DTAP002A
/// </summary>
/// <remarks>
/// <para>
/// API Documentation: https://financialmodelingprep.com/developer/docs
/// Base URL: https://financialmodelingprep.com/api
/// Authentication: API key as query parameter
/// Free tier: 250 calls/day
/// </para>
/// </remarks>
public interface IFinancialModelingPrepApi
{
    /// <summary>
    /// Gets earnings calendar for a date range.
    /// </summary>
    [Get("/v3/earnings-calendar")]
    Task<FmpEarningsEvent[]> GetEarningsCalendarAsync(
        [AliasAs("from")] string fromDate,
        [AliasAs("to")] string toDate,
        [AliasAs("apikey")] string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical earnings for a specific symbol.
    /// </summary>
    [Get("/v3/historical/earning_calendar/{symbol}")]
    Task<FmpEarningsEvent[]> GetHistoricalEarningsAsync(
        string symbol,
        [AliasAs("apikey")] string apiKey,
        CancellationToken cancellationToken = default);
}

#region Response DTOs

public sealed class FmpEarningsEvent
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
