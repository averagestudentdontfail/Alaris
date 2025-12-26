// DTRS002A.cs - Refit service registration extensions

using System;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Alaris.Infrastructure.Data.Http.Contracts;

namespace Alaris.Infrastructure.Data.Http;

/// <summary>
/// Service registration extensions for Refit API clients.
/// Component ID: DTRS002A
/// </summary>
public static class RefitServiceExtensions
{
    private const string DefaultUserAgent = "Alaris/1.0 (Quantitative Trading System)";

    /// <summary>
    /// Adds all Alaris API clients with resilience policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration containing API keys.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAlarisApiClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Polygon API
        services.AddRefitClient<IPolygonApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.polygon.io");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
            })
            .AddAlarisResilience();

        // Financial Modeling Prep API  
        services.AddRefitClient<IFinancialModelingPrepApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://financialmodelingprep.com/api");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
            })
            .AddAlarisResilience();

        // Treasury Direct API
        services.AddRefitClient<ITreasuryDirectApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://www.treasurydirect.gov/TA_WS/securities");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
            })
            .AddAlarisResilience();

        // NASDAQ Calendar API (requires browser-like headers)
        services.AddRefitClient<INasdaqCalendarApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.nasdaq.com");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddAlarisResilience();

        return services;
    }
}
