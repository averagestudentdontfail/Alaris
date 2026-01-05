// CRTM004A.cs - TimeProvider DI registration extensions

using Microsoft.Extensions.DependencyInjection;

namespace Alaris.Core.Time;

/// <summary>
/// Extension methods for registering time providers with dependency injection.
/// </summary>
public static class TimeProviderExtensions
{
    /// <summary>
    /// Registers the live time provider for real-time trading.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLiveTimeProvider(this IServiceCollection services)
    {
        return services.AddSingleton<ITimeProvider, LiveTimeProvider>();
    }

    /// <summary>
    /// Registers a backtest time provider with a simulated time source.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="timeSource">
    /// A delegate returning the current simulated DateTime (from LEAN's algorithm.Time).
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBacktestTimeProvider(
        this IServiceCollection services,
        Func<DateTime> timeSource)
    {
        return services.AddSingleton<ITimeProvider>(
            _ => new BacktestTimeProvider(timeSource));
    }

    /// <summary>
    /// Registers a backtest time provider with a fixed simulated time.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="fixedTime">The fixed simulated time to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBacktestTimeProvider(
        this IServiceCollection services,
        DateTime fixedTime)
    {
        return services.AddSingleton<ITimeProvider>(
            _ => new BacktestTimeProvider(fixedTime));
    }
}
