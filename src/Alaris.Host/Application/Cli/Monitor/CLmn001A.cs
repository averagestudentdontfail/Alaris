// CLmn001A.cs - Algorithm monitor interface for LEAN integration

namespace Alaris.Host.Application.Cli.Monitor;

/// <summary>
/// Interface for monitoring algorithm execution (backtest or live).
/// Provides observable streams of equity, trades, and performance metrics.
/// Component ID: CLmn001A
/// </summary>
public interface CLmn001A
{
    /// <summary>
    /// Gets whether the algorithm is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current algorithm mode.
    /// </summary>
    AlgorithmMode Mode { get; }

    /// <summary>
    /// Raised when equity value changes.
    /// </summary>
    event EventHandler<EquityUpdate>? EquityChanged;

    /// <summary>
    /// Raised when a trade is executed.
    /// </summary>
    event EventHandler<TradeUpdate>? TradeExecuted;

    /// <summary>
    /// Raised when algorithm status changes.
    /// </summary>
    event EventHandler<StatusUpdate>? StatusChanged;

    /// <summary>
    /// Starts monitoring the algorithm.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Algorithm execution mode.
/// </summary>
public enum AlgorithmMode
{
    Backtest,
    Paper,
    Live
}

/// <summary>
/// Equity value update event.
/// </summary>
public sealed class EquityUpdate : EventArgs
{
    public DateTime Timestamp { get; }
    public decimal Equity { get; }
    public decimal Cash { get; }
    public decimal Holdings { get; }
    public decimal UnrealizedPnL { get; }
    public decimal RealizedPnL { get; }

    public EquityUpdate(DateTime timestamp, decimal equity, decimal cash, decimal holdings, decimal unrealizedPnL, decimal realizedPnL)
    {
        Timestamp = timestamp;
        Equity = equity;
        Cash = cash;
        Holdings = holdings;
        UnrealizedPnL = unrealizedPnL;
        RealizedPnL = realizedPnL;
    }
}

/// <summary>
/// Trade execution event.
/// </summary>
public sealed class TradeUpdate : EventArgs
{
    public DateTime Timestamp { get; }
    public string Symbol { get; }
    public string Direction { get; }
    public decimal Quantity { get; }
    public decimal FillPrice { get; }
    public decimal Commission { get; }
    public decimal PnL { get; }

    public TradeUpdate(DateTime timestamp, string symbol, string direction, decimal quantity, decimal fillPrice, decimal commission, decimal pnl)
    {
        Timestamp = timestamp;
        Symbol = symbol;
        Direction = direction;
        Quantity = quantity;
        FillPrice = fillPrice;
        Commission = commission;
        PnL = pnl;
    }
}

/// <summary>
/// Algorithm status update.
/// </summary>
public sealed class StatusUpdate : EventArgs
{
    public DateTime Timestamp { get; }
    public string Status { get; }
    public double ProgressPercent { get; }
    public TimeSpan? EstimatedRemaining { get; }

    public StatusUpdate(DateTime timestamp, string status, double progressPercent, TimeSpan? estimatedRemaining)
    {
        Timestamp = timestamp;
        Status = status;
        ProgressPercent = progressPercent;
        EstimatedRemaining = estimatedRemaining;
    }
}
