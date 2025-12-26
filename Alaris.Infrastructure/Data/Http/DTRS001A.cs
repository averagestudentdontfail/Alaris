// DTRS001A.cs - HTTP resilience configuration for Alaris API clients

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Alaris.Infrastructure.Data.Http;

/// <summary>
/// HTTP resilience configuration for all API clients.
/// Component ID: DTRS001A
/// </summary>
/// <remarks>
/// <para>
/// Uses Microsoft.Extensions.Http.Resilience standard handler with:
/// - Retry: 3 attempts with exponential backoff + jitter
/// - Circuit breaker: 10% failure ratio, 5s break duration
/// - Timeouts: 10s per-attempt, 30s total
/// </para>
/// <para>
/// Based on Microsoft.Extensions.Http.Resilience best practices.
/// </para>
/// </remarks>
public static class HttpResilienceConfiguration
{
    /// <summary>
    /// Adds standard Alaris resilience policies to an HTTP client builder.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The resilience pipeline builder for further configuration.</returns>
    public static IHttpStandardResiliencePipelineBuilder AddAlarisResilience(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // AddStandardResilienceHandler provides:
        // - Total timeout: 30s (entire operation including retries)
        // - Retry: 3 attempts, exponential backoff with jitter
        // - Circuit breaker: 10% failure ratio, 5s break duration  
        // - Attempt timeout: 10s per individual request
        return builder.AddStandardResilienceHandler();
    }

    /// <summary>
    /// Adds Alaris resilience with custom configuration.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="configure">Action to configure resilience options.</param>
    /// <returns>The resilience pipeline builder for further configuration.</returns>
    public static IHttpStandardResiliencePipelineBuilder AddAlarisResilience(
        this IHttpClientBuilder builder,
        Action<HttpStandardResilienceOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return builder.AddStandardResilienceHandler(configure);
    }
}
