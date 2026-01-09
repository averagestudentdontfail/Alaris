// HTRL001A.cs - Shared API rate limiter for external service calls

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Alaris.Infrastructure.Http;

/// <summary>
/// Shared rate limiter for external API calls.
/// Component ID: HTRL001A
/// </summary>
/// <remarks>
/// <para>
/// Implements token bucket rate limiting with configurable requests per second
/// and maximum concurrent connections. Designed for reuse across multiple
/// API clients (Polygon, NASDAQ, etc.).
/// </para>
/// <para>
/// Thread-safe: uses ConcurrentQueue and SemaphoreSlim for coordination.
/// </para>
/// <para>
/// Governance Compliance:
/// - Rule 11: Thread-safe shared state via ConcurrentQueue
/// - Rule 19: Structured logging with named parameters
/// - Rule 22: Configurable via constructor parameters
/// </para>
/// </remarks>
public sealed class ApiRateLimiter : IAsyncDisposable
{
    private readonly int _requestsPerSecond;
    private readonly int _maxConcurrentRequests;
    private readonly TimeSpan _requestInterval;
    private readonly ConcurrentQueue<TaskCompletionSource<bool>> _pendingRequests;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly ILogger<ApiRateLimiter> _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Task _schedulerTask;
    private readonly string _providerName;
    private bool _disposed;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApiRateLimiter"/> class.
    /// </summary>
    /// <param name="providerName">Name of the API provider (for logging).</param>
    /// <param name="requestsPerSecond">Maximum requests per second (default: 100).</param>
    /// <param name="maxConcurrentRequests">Maximum concurrent requests (default: 25).</param>
    /// <param name="logger">Logger instance.</param>
    public ApiRateLimiter(
        string providerName,
        int requestsPerSecond,
        int maxConcurrentRequests,
        ILogger<ApiRateLimiter> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestsPerSecond);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentRequests);
        ArgumentNullException.ThrowIfNull(logger);

        _providerName = providerName;
        _requestsPerSecond = requestsPerSecond;
        _maxConcurrentRequests = maxConcurrentRequests;
        _requestInterval = TimeSpan.FromSeconds(1.0 / requestsPerSecond);
        _pendingRequests = new ConcurrentQueue<TaskCompletionSource<bool>>();
        _concurrencyGate = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        _logger = logger;
        _cts = new CancellationTokenSource();

        _logger.LogInformation(
            "{Provider} rate limiter initialized: {RPS} req/s, {Concurrent} concurrent",
            _providerName, _requestsPerSecond, _maxConcurrentRequests);

        // Start the scheduler background task
        _schedulerTask = RunSchedulerAsync(_cts.Token);
    }

    /// <summary>
    /// Gets the configured requests per second.
    /// </summary>
    public int RequestsPerSecond => _requestsPerSecond;

    /// <summary>
    /// Gets the configured maximum concurrent requests.
    /// </summary>
    public int MaxConcurrentRequests => _maxConcurrentRequests;

    /// <summary>
    /// Gets the number of pending requests in the queue.
    /// </summary>
    public int PendingCount => _pendingRequests.Count;

    /// <summary>
    /// Acquires a rate-limited slot for making an API request.
    /// Returns an IDisposable that must be disposed when the request completes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A disposable token that releases the concurrency slot when disposed.</returns>
    public async ValueTask<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Wait for rate limit slot
        TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration reg = cancellationToken.Register(
            () => gate.TrySetCanceled(cancellationToken));

        _pendingRequests.Enqueue(gate);

        await gate.Task.ConfigureAwait(false);

        // Wait for concurrency slot
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        return new RateLimitToken(this);
    }

    /// <summary>
    /// Runs the scheduler loop that releases pending requests according to rate limit.
    /// </summary>
    private async Task RunSchedulerAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_requestInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_pendingRequests.TryDequeue(out TaskCompletionSource<bool>? gate) && gate != null)
                {
                    gate.TrySetResult(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown - release all pending requests
            while (_pendingRequests.TryDequeue(out TaskCompletionSource<bool>? gate))
            {
                gate?.TrySetCanceled(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Releases a concurrency slot after request completion.
    /// </summary>
    private void Release()
    {
        _concurrencyGate.Release();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _schedulerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        _cts.Dispose();
        _concurrencyGate.Dispose();

        _logger.LogInformation("{Provider} rate limiter disposed", _providerName);
    }

    /// <summary>
    /// Token returned by AcquireAsync that releases the concurrency slot when disposed.
    /// </summary>
    private sealed class RateLimitToken : IDisposable
    {
        private readonly ApiRateLimiter _limiter;
        private bool _disposed;

        public RateLimitToken(ApiRateLimiter limiter)
        {
            _limiter = limiter;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _limiter.Release();
        }
    }
}
