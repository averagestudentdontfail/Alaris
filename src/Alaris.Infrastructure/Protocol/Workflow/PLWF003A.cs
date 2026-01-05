// PLWF003A.cs - Trading workflow FSM definition

namespace Alaris.Infrastructure.Protocol.Workflow;

/// <summary>
/// Trading workflow states.
/// </summary>
public enum TradingState
{
    /// <summary>Not connected to broker.</summary>
    Disconnected,
    
    /// <summary>Attempting to connect to broker.</summary>
    Connecting,
    
    /// <summary>Connected to broker, ready to trade.</summary>
    Ready,
    
    /// <summary>Evaluating market conditions and generating signals.</summary>
    Evaluating,
    
    /// <summary>Executing a trade order.</summary>
    Executing,
    
    /// <summary>Trade executed, monitoring position.</summary>
    Monitoring,
    
    /// <summary>Fatal error occurred (terminal).</summary>
    Error
}

/// <summary>
/// Trading workflow events.
/// </summary>
public enum TradingEvent
{
    /// <summary>Initiate broker connection.</summary>
    Connect,
    
    /// <summary>Connection established successfully.</summary>
    Connected,
    
    /// <summary>Connection failed.</summary>
    ConnectionFailed,
    
    /// <summary>Disconnect from broker.</summary>
    Disconnect,
    
    /// <summary>Begin market evaluation.</summary>
    Evaluate,
    
    /// <summary>Signal generated, initiate trade.</summary>
    SignalGenerated,
    
    /// <summary>No signal, return to ready state.</summary>
    NoSignal,
    
    /// <summary>Trade execution completed.</summary>
    TradeExecuted,
    
    /// <summary>Trade execution failed.</summary>
    TradeFailed,
    
    /// <summary>Position closed (manual or automatic).</summary>
    PositionClosed,
    
    /// <summary>Unrecoverable error occurred.</summary>
    FatalError,
    
    /// <summary>Reset from error state.</summary>
    Reset
}

/// <summary>
/// Factory for creating the Trading workflow FSM.
/// Component ID: PLWF003A
/// </summary>
public static class PLWF003A
{
    /// <summary>
    /// Creates a new Trading workflow FSM.
    /// </summary>
    /// <returns>A configured FSM instance starting in Disconnected state.</returns>
    public static PLWF001A<TradingState, TradingEvent> Create()
    {
        return new PLWF001A<TradingState, TradingEvent>(TradingState.Disconnected)
            // === Connection Management ===
            .AddTransition(
                TradingState.Disconnected,
                TradingEvent.Connect,
                TradingState.Connecting)
            .AddTransition(
                TradingState.Connecting,
                TradingEvent.Connected,
                TradingState.Ready)
            .AddTransition(
                TradingState.Connecting,
                TradingEvent.ConnectionFailed,
                TradingState.Disconnected)
            
            // === Evaluation Loop ===
            .AddTransition(
                TradingState.Ready,
                TradingEvent.Evaluate,
                TradingState.Evaluating)
            .AddTransition(
                TradingState.Ready,
                TradingEvent.Disconnect,
                TradingState.Disconnected)
            .AddTransition(
                TradingState.Evaluating,
                TradingEvent.SignalGenerated,
                TradingState.Executing)
            .AddTransition(
                TradingState.Evaluating,
                TradingEvent.NoSignal,
                TradingState.Ready)
            
            // === Trade Execution ===
            .AddTransition(
                TradingState.Executing,
                TradingEvent.TradeExecuted,
                TradingState.Monitoring)
            .AddTransition(
                TradingState.Executing,
                TradingEvent.TradeFailed,
                TradingState.Ready)
            
            // === Position Monitoring ===
            .AddTransition(
                TradingState.Monitoring,
                TradingEvent.PositionClosed,
                TradingState.Ready)
            .AddTransition(
                TradingState.Monitoring,
                TradingEvent.Evaluate,
                TradingState.Evaluating)  // Allow re-evaluation while monitoring
            
            // === Error Handling ===
            .AddTransition(
                TradingState.Ready,
                TradingEvent.FatalError,
                TradingState.Error)
            .AddTransition(
                TradingState.Evaluating,
                TradingEvent.FatalError,
                TradingState.Error)
            .AddTransition(
                TradingState.Executing,
                TradingEvent.FatalError,
                TradingState.Error)
            .AddTransition(
                TradingState.Monitoring,
                TradingEvent.FatalError,
                TradingState.Error)
            .AddTransition(
                TradingState.Error,
                TradingEvent.Reset,
                TradingState.Disconnected)
            
            // === Terminal State ===
            .SetTerminal(TradingState.Error);
    }
}
