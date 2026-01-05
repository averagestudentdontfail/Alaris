// DTAP004A.cs - NASDAQ Calendar API contract

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Alaris.Infrastructure.Data.Http.Contracts;

/// <summary>
/// NASDAQ public calendar REST API contract.
/// Component ID: DTAP004A
/// </summary>
/// <remarks>
/// <para>
/// Base URL: https://api.nasdaq.com
/// Authentication: None required (browser-like headers needed)
/// Rate Limits: Undocumented (appears unlimited for reasonable use)
/// </para>
/// <para>
/// Fail-fast design: No fallback providers. Errors propagate immediately.
/// Future Alternative: EODHD Calendar API ($19.99/month) with official C# wrapper.
/// </para>
/// </remarks>
public interface INasdaqCalendarApi
{
    /// <summary>
    /// Gets earnings calendar for a specific date.
    /// </summary>
    [Get("/api/calendar/earnings")]
    Task<NasdaqEarningsResponse> GetEarningsAsync(
        [AliasAs("date")] string dateValue,
        CancellationToken cancellationToken = default);
}

#region Response DTOs

public sealed class NasdaqEarningsResponse
{
    [JsonPropertyName("data")]
    public NasdaqEarningsData? Data { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public NasdaqStatus? Status { get; init; }
}

public sealed class NasdaqEarningsData
{
    [JsonPropertyName("rows")]
    public IReadOnlyList<NasdaqEarningsRow>? Rows { get; init; }

    [JsonPropertyName("headers")]
    public NasdaqHeaders? Headers { get; init; }
}

public sealed class NasdaqHeaders
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed class NasdaqStatus
{
    [JsonPropertyName("rCode")]
    public int RCode { get; init; }

    /// <summary>
    /// NASDAQ API returns this as either a string or an array.
    /// Using JsonElement for flexible deserialization.
    /// </summary>
    [JsonPropertyName("bCodeMessage")]
    public System.Text.Json.JsonElement? BCodeMessage { get; init; }
}

public sealed class NasdaqEarningsRow
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("marketCap")]
    public string? MarketCap { get; init; }

    [JsonPropertyName("fiscalQuarterEnding")]
    public string? FiscalQuarterEnding { get; init; }

    [JsonPropertyName("epsForecast")]
    public string? EpsForecast { get; init; }

    [JsonPropertyName("noOfEsts")]
    public string? NumberOfEstimates { get; init; }

    [JsonPropertyName("lastYearRptDt")]
    public string? LastYearReportDate { get; init; }

    [JsonPropertyName("lastYearEPS")]
    public string? LastYearEps { get; init; }

    [JsonPropertyName("time")]
    public string? Time { get; init; }

    [JsonPropertyName("eps")]
    public string? EpsActual { get; init; }
}

#endregion
