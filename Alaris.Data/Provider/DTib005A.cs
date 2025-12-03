using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Alaris.Data.Model;

namespace Alaris.Data.Provider.Interactive;

/// <summary>
/// Interactive Brokers snapshot quote provider for real-time execution pricing.
/// Component ID: DTib005A
/// </summary>
/// <remarks>
/// Provides on-demand snapshot quotes using IBKR Gateway API.
/// 
/// Cost: ~$0.01-0.03 per snapshot (no subscription required)
/// Usage: Request real-time quote immediately before order execution
/// 
/// Advantages:
/// - No subscription waste (pay per use)
/// - Real-time accuracy at execution moment
/// - No minimum account equity for snapshots
/// - Simple integration with existing IB Gateway
/// 
/// Implementation notes:
/// - Uses IBApi (SWIG C# bindings)
/// - Requires active IB Gateway connection
/// - Snapshot flag = true (non-streaming)
/// - Waits for tickPrice callbacks
/// </remarks>
public sealed class InteractiveBrokersSnapshotProvider : IExecutionQuoteProvider, IDisposable
{
    private readonly ILogger<InteractiveBrokersSnapshotProvider> _logger;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<OptionContract>> _pendingRequests;
    private int _nextRequestId;
    private bool _disposed;

    // NOTE: IBApi types are commented out as they require SWIG-generated bindings
    // In actual implementation, add reference to IBApi.dll and uncomment
    // private readonly IBApi.EClientSocket _client;
    // private readonly IBApi.EWrapper _wrapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveBrokersSnapshotProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public InteractiveBrokersSnapshotProvider(
        ILogger<InteractiveBrokersSnapshotProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<OptionContract>>();
        _nextRequestId = 1000;

        // TODO: Initialize IB Gateway connection
        // _wrapper = new IbWrapper(this);
        // _client = new IBApi.EClientSocket(_wrapper);
        // _client.eConnect(host, port, clientId);
    }

    /// <inheritdoc/>
    public async Task<OptionContract> GetSnapshotQuoteAsync(
        string underlyingSymbol,
        decimal strike,
        DateTime expiration,
        OptionRight right,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(underlyingSymbol))
            throw new ArgumentException("Underlying symbol cannot be null or whitespace", nameof(underlyingSymbol));

        _logger.LogInformation(
            "Requesting snapshot quote for {Symbol} {Strike} {Right} expiring {Expiration:yyyy-MM-dd}",
            underlyingSymbol, strike, right, expiration);

        var reqId = Interlocked.Increment(ref _nextRequestId);
        var tcs = new TaskCompletionSource<OptionContract>();
        
        if (!_pendingRequests.TryAdd(reqId, tcs))
            throw new InvalidOperationException($"Request ID {reqId} already in use");

        try
        {
            // Create IB contract
            var contract = CreateOptionContract(underlyingSymbol, strike, expiration, right);

            // Request snapshot (non-streaming)
            // _client.reqMktData(
            //     reqId,
            //     contract,
            //     genericTickList: "100,101", // Bid (100), Ask (101)
            //     snapshot: true,              // Key: one-time quote
            //     regulatorySnapshot: false,
            //     mktDataOptions: null
            // );

            // Wait for callback with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var quote = await tcs.Task.WaitAsync(timeoutCts.Token);

            _logger.LogInformation(
                "Received snapshot quote for {Symbol}: Bid={Bid}, Ask={Ask}",
                underlyingSymbol, quote.Bid, quote.Ask);

            return quote;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Snapshot quote request timed out for {Symbol}", underlyingSymbol);
            throw new TimeoutException($"Snapshot quote request timed out after 10 seconds");
        }
        finally
        {
            _pendingRequests.TryRemove(reqId, out _);
        }
    }

    /// <inheritdoc/>
    public async Task<CalendarSpreadQuote> GetCalendarSpreadQuoteAsync(
        string underlyingSymbol,
        decimal strike,
        DateTime frontExpiration,
        DateTime backExpiration,
        OptionRight right,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Requesting calendar spread quote for {Symbol} {Strike} {Right}",
            underlyingSymbol, strike, right);

        // Request both legs concurrently
        var frontTask = GetSnapshotQuoteAsync(
            underlyingSymbol,
            strike,
            frontExpiration,
            right,
            cancellationToken);

        var backTask = GetSnapshotQuoteAsync(
            underlyingSymbol,
            strike,
            backExpiration,
            right,
            cancellationToken);

        await Task.WhenAll(frontTask, backTask);

        var frontLeg = await frontTask;
        var backLeg = await backTask;

        // Calculate spread pricing
        // Buy back month, sell front month (debit spread)
        var spreadBid = backLeg.Bid - frontLeg.Ask; // What we pay
        var spreadAsk = backLeg.Ask - frontLeg.Bid; // What we receive if reversing

        var calendarSpread = new CalendarSpreadQuote
        {
            UnderlyingSymbol = underlyingSymbol,
            Strike = strike,
            FrontLeg = frontLeg,
            BackLeg = backLeg,
            SpreadBid = spreadBid,
            SpreadAsk = spreadAsk,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Calendar spread quote: Bid={Bid:F2}, Ask={Ask:F2}, Mid={Mid:F2}, Width={Width:F2}",
            calendarSpread.SpreadBid,
            calendarSpread.SpreadAsk,
            calendarSpread.SpreadMid,
            calendarSpread.SpreadWidth);

        return calendarSpread;
    }

    /// <summary>
    /// Internal callback handler for tickPrice events from IB Gateway.
    /// Called by EWrapper when quote data arrives.
    /// </summary>
    internal void OnTickPrice(int reqId, int field, decimal price)
    {
        // This would be called by IBApi.EWrapper.tickPrice()
        // Field codes: 1=Bid, 2=Ask, 4=Last, etc.
        
        if (!_pendingRequests.TryGetValue(reqId, out var tcs))
            return;

        // For snapshot implementation, accumulate bid/ask then complete
        // In production, track bid/ask separately and complete when both received
        
        _logger.LogDebug("Received tick price for request {ReqId}: field={Field}, price={Price}",
            reqId, field, price);

        // TODO: Implement full quote assembly logic
        // When both bid and ask received, construct OptionContract and complete TCS
    }

    /// <summary>
    /// Creates an IB contract specification for an option.
    /// </summary>
    private static object CreateOptionContract(
        string underlyingSymbol,
        decimal strike,
        DateTime expiration,
        OptionRight right)
    {
        // TODO: Replace with actual IBApi.Contract creation
        // var contract = new IBApi.Contract
        // {
        //     Symbol = underlyingSymbol,
        //     SecType = "OPT",
        //     Exchange = "SMART",
        //     Currency = "USD",
        //     Strike = (double)strike,
        //     LastTradeDateOrContractMonth = expiration.ToString("yyyyMMdd"),
        //     Right = right == OptionRight.Call ? "C" : "P"
        // };
        
        return new
        {
            Symbol = underlyingSymbol,
            SecType = "OPT",
            Strike = strike,
            Expiration = expiration,
            Right = right
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Disconnect IB Gateway
        // if (_client?.IsConnected() == true)
        // {
        //     _client.eDisconnect();
        // }

        // Complete any pending requests with cancellation
        foreach (var kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }
        
        _pendingRequests.Clear();

        _logger.LogInformation("InteractiveBrokersSnapshotProvider disposed");
    }
}

/// <summary>
/// IB Gateway wrapper implementation (EWrapper interface).
/// Receives callbacks from IB Gateway API.
/// </summary>
file sealed class IbWrapper // : IBApi.EWrapper
{
    private readonly InteractiveBrokersSnapshotProvider _provider;

    public IbWrapper(InteractiveBrokersSnapshotProvider provider)
    {
        _provider = provider;
    }

    // TODO: Implement required EWrapper methods
    // public void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
    // {
    //     _provider.OnTickPrice(tickerId, field, (decimal)price);
    // }

    // ... other required EWrapper callbacks
}