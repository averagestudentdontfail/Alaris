// DTsv001A.cs - Infrastructure service registration with HTTP resilience

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Alaris.Infrastructure.Data;
using Alaris.Infrastructure.Data.Provider;
using Alaris.Infrastructure.Data.Provider.Treasury;
using Alaris.Infrastructure.Data.Provider.Polygon;
using System;

namespace Alaris.Infrastructure;

/// <summary>
/// Service collection extensions for Alaris.Infrastructure.
/// Component ID: DTsv001A
/// </summary>
/// <remarks>
/// Provides centralized service registration with production-grade HTTP resilience patterns:
/// <list type="bullet">
///   <item><description>Exponential backoff retry (3 attempts)</description></item>
///   <item><description>Circuit breaker (5 failures, 30 second break)</description></item>
///   <item><description>Request timeout (30 seconds per attempt)</description></item>
///   <item><description>Memory caching for API responses</description></item>
/// </list>
/// Based on Microsoft.Extensions.Http.Resilience (Polly v8).
/// </remarks>
public static class DTsv001A
{
    private const string UserAgent = "AlarisTradingSystem/1.0";

    /// <summary>
    /// Adds infrastructure services with resilient HTTP clients and caching.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAlarisInfrastructure(this IServiceCollection services)
    {
        // Add memory cache
        services.AddMemoryCache();
        services.AddSingleton<DTch001A>();

        // Register TreasuryDirectRateProvider with resilience
        services.AddHttpClient<TreasuryDirectRateProvider>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        })
        .AddStandardResilienceHandler();

        // Register PolygonApiClient with resilience
        services.AddHttpClient<PolygonApiClient>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        })
        .AddStandardResilienceHandler();

        // Register PolygonUniverseProvider with resilience
        services.AddHttpClient<PolygonUniverseProvider>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        })
        .AddStandardResilienceHandler();

        return services;
    }

    /// <summary>
    /// Adds typed configuration options from the configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAlarisConfiguration(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // Bind typed options from configuration sections
        services.Configure<Configuration.PolygonOptions>(
            configuration.GetSection(Configuration.PolygonOptions.SectionName));
        services.Configure<Configuration.TreasuryOptions>(
            configuration.GetSection(Configuration.TreasuryOptions.SectionName));
        services.Configure<Configuration.SecEdgarOptions>(
            configuration.GetSection(Configuration.SecEdgarOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Adds a named HTTP client with standard resilience policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The client name.</param>
    /// <param name="configureClient">Optional client configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilientHttpClient(
        this IServiceCollection services,
        string name,
        Action<HttpClient>? configureClient = null)
    {
        IHttpClientBuilder builder = services.AddHttpClient(name);

        if (configureClient != null)
        {
            builder.ConfigureHttpClient(configureClient);
        }

        builder.AddStandardResilienceHandler();

        return services;
    }
}
