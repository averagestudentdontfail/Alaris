// DTAP001A.cs - Polygon.io API contract

using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Alaris.Infrastructure.Data.Http.Contracts;

/// <summary>
/// Polygon.io REST API contract.
/// Component ID: DTAP001A
/// </summary>
/// <remarks>
/// <para>
/// API Documentation: https://polygon.io/docs
/// Base URL: https://api.polygon.io
/// Authentication: API key as query parameter
/// </para>
/// </remarks>
public interface IPolygonApi
{
    /// <summary>
    /// Gets historical daily aggregates (OHLCV bars).
    /// </summary>
    [Get("/v2/aggs/ticker/{ticker}/range/1/day/{fromDate}/{toDate}")]
    Task<PolygonAggregatesResponse> GetDailyBarsAsync(
        string ticker,
        string fromDate,
        string toDate,
        [AliasAs("adjusted")] bool adjusted,
        [AliasAs("sort")] string sort,
        [AliasAs("apiKey")] string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets previous day's aggregate (for spot price).
    /// </summary>
    [Get("/v2/aggs/ticker/{ticker}/prev")]
    Task<PolygonAggregatesResponse> GetPreviousDayAsync(
        string ticker,
        [AliasAs("adjusted")] bool adjusted,
        [AliasAs("apiKey")] string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets option contracts reference data.
    /// </summary>
    [Get("/v3/reference/options/contracts")]
    Task<PolygonOptionsContractsResponse> GetOptionsContractsAsync(
        [AliasAs("underlying_ticker")] string underlyingTicker,
        [AliasAs("as_of")] string asOfDate,
        [AliasAs("expiration_date.gte")] string expirationMin,
        [AliasAs("expiration_date.lte")] string expirationMax,
        [AliasAs("limit")] int limit,
        [AliasAs("apiKey")] string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregates for a specific ticker (used for option contract pricing).
    /// </summary>
    [Get("/v2/aggs/ticker/{ticker}/range/1/day/{fromDate}/{toDate}")]
    Task<PolygonAggregatesResponse> GetTickerAggregatesAsync(
        string ticker,
        string fromDate,
        string toDate,
        [AliasAs("adjusted")] bool adjusted,
        [AliasAs("apiKey")] string apiKey,
        CancellationToken cancellationToken = default);
}

#region Response DTOs

public sealed class PolygonAggregatesResponse
{
    [JsonPropertyName("results")]
    public PolygonBar[]? Results { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("resultsCount")]
    public int ResultsCount { get; init; }
}

public sealed class PolygonBar
{
    [JsonPropertyName("t")]
    public long Timestamp { get; init; }

    [JsonPropertyName("o")]
    public decimal Open { get; init; }

    [JsonPropertyName("h")]
    public decimal High { get; init; }

    [JsonPropertyName("l")]
    public decimal Low { get; init; }

    [JsonPropertyName("c")]
    public decimal Close { get; init; }

    [JsonPropertyName("v")]
    public double Volume { get; init; }
}

public sealed class PolygonOptionsContractsResponse
{
    [JsonPropertyName("results")]
    public PolygonOptionContract[]? Results { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

public sealed class PolygonOptionContract
{
    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [JsonPropertyName("underlying_ticker")]
    public required string UnderlyingTicker { get; init; }

    [JsonPropertyName("expiration_date")]
    public string? ExpirationDate { get; init; }

    [JsonPropertyName("strike_price")]
    public decimal StrikePrice { get; init; }

    [JsonPropertyName("contract_type")]
    public string? ContractType { get; init; }
}

#endregion
