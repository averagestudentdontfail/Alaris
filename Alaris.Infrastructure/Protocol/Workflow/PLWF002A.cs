// PLWF002A.cs - Backtest workflow FSM definition

namespace Alaris.Infrastructure.Protocol.Workflow;

/// <summary>
/// Backtest workflow states.
/// </summary>
/// <remarks>
/// State diagram:
/// <code>
///                    ┌──────────────────────────────────┐
///                    │               Reset              │
///                    ▼                                  │
///   [Idle] ──SelectSession──▶ [SessionSelected]         │
///     │                              │                  │
///     │ CreateSession               CheckData           │
///     ▼                              ▼                  │
///   [SessionSelected] ◀── [DataChecking]                │
///                           │         │                 │
///                    DataMissing   DataReady            │
///                           ▼         │                 │
///                  [DataBootstrapping]│                 │
///                    │         │      │                 │
///            Complete│    Failed│     │                 │
///                    ▼         ▼      ▼                 │
///                  [DataChecking] ◀───────────────────  │
///                           │                           │
///                        StartLean                      │
///                           ▼                           │
///                    [ExecutingLean]                    │
///                      │         │                      │
///               Completed    Failed                     │
///                      ▼         ▼                      │
///                [Completed]  [Failed] ─────Reset───────┘
/// </code>
/// </remarks>
public enum BacktestState
{
    /// <summary>No session selected, idle state.</summary>
    Idle,
    
    /// <summary>A session has been selected or created.</summary>
    SessionSelected,
    
    /// <summary>Checking data availability for the session.</summary>
    DataChecking,
    
    /// <summary>Bootstrapping missing data (prices/earnings).</summary>
    DataBootstrapping,
    
    /// <summary>Executing LEAN engine with the session.</summary>
    ExecutingLean,
    
    /// <summary>Monitoring LEAN execution.</summary>
    Monitoring,
    
    /// <summary>Backtest completed successfully (terminal).</summary>
    Completed,
    
    /// <summary>Backtest failed (terminal).</summary>
    Failed
}

/// <summary>
/// Backtest workflow events/actions.
/// </summary>
public enum BacktestEvent
{
    /// <summary>User selects an existing session.</summary>
    SelectSession,
    
    /// <summary>User creates a new session.</summary>
    CreateSession,
    
    /// <summary>Begin checking data availability.</summary>
    CheckData,
    
    /// <summary>Data check determined data is missing.</summary>
    DataMissing,
    
    /// <summary>Data check determined data is ready.</summary>
    DataReady,
    
    /// <summary>Begin bootstrapping missing data.</summary>
    BootstrapData,
    
    /// <summary>Data bootstrap completed successfully.</summary>
    BootstrapComplete,
    
    /// <summary>Data bootstrap failed.</summary>
    BootstrapFailed,
    
    /// <summary>Start LEAN engine execution.</summary>
    StartLean,
    
    /// <summary>LEAN execution completed successfully.</summary>
    LeanCompleted,
    
    /// <summary>LEAN execution failed.</summary>
    LeanFailed,
    
    /// <summary>View backtest results.</summary>
    ViewResults,
    
    /// <summary>Reset workflow to idle state.</summary>
    Reset
}

/// <summary>
/// Factory for creating the Backtest workflow FSM.
/// Component ID: PLWF002A
/// </summary>
/// <remarks>
/// This factory creates a fully-configured FSM for the backtest workflow.
/// The FSM enforces valid state transitions and provides guards for
/// precondition validation.
/// </remarks>
public static class PLWF002A
{
    /// <summary>
    /// Creates a new Backtest workflow FSM with all transitions defined.
    /// </summary>
    /// <returns>A configured FSM instance starting in Idle state.</returns>
    public static PLWF001A<BacktestState, BacktestEvent> Create()
    {
        return new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            // === From Idle ===
            .AddTransition(
                BacktestState.Idle, 
                BacktestEvent.SelectSession, 
                BacktestState.SessionSelected)
            .AddTransition(
                BacktestState.Idle, 
                BacktestEvent.CreateSession, 
                BacktestState.SessionSelected)
            
            // === From SessionSelected ===
            .AddTransition(
                BacktestState.SessionSelected, 
                BacktestEvent.CheckData, 
                BacktestState.DataChecking)
            .AddTransition(
                BacktestState.SessionSelected,
                BacktestEvent.Reset,
                BacktestState.Idle)
            
            // === From DataChecking ===
            .AddTransition(
                BacktestState.DataChecking, 
                BacktestEvent.DataMissing, 
                BacktestState.DataBootstrapping)
            .AddTransition(
                BacktestState.DataChecking, 
                BacktestEvent.DataReady, 
                BacktestState.ExecutingLean)
            
            // === From DataBootstrapping ===
            .AddTransition(
                BacktestState.DataBootstrapping, 
                BacktestEvent.BootstrapComplete, 
                BacktestState.DataChecking)  // Re-check after bootstrap
            .AddTransition(
                BacktestState.DataBootstrapping, 
                BacktestEvent.BootstrapFailed, 
                BacktestState.Failed)
            
            // === From ExecutingLean ===
            .AddTransition(
                BacktestState.ExecutingLean, 
                BacktestEvent.LeanCompleted, 
                BacktestState.Completed)
            .AddTransition(
                BacktestState.ExecutingLean, 
                BacktestEvent.LeanFailed, 
                BacktestState.Failed)
            
            // === Terminal State Reset Paths ===
            .AddTransition(
                BacktestState.Completed, 
                BacktestEvent.Reset, 
                BacktestState.Idle)
            .AddTransition(
                BacktestState.Failed, 
                BacktestEvent.Reset, 
                BacktestState.Idle)
            
            // === Terminal States ===
            .SetTerminal(BacktestState.Completed)
            .SetTerminal(BacktestState.Failed);
    }
    
    /// <summary>
    /// Creates a Backtest FSM with custom actions attached to transitions.
    /// </summary>
    /// <param name="onDataMissing">Action when data is detected as missing.</param>
    /// <param name="onBootstrapComplete">Action when bootstrap succeeds.</param>
    /// <param name="onLeanCompleted">Action when LEAN execution completes.</param>
    /// <param name="onLeanFailed">Action when LEAN execution fails.</param>
    /// <returns>A configured FSM with actions.</returns>
    public static PLWF001A<BacktestState, BacktestEvent> CreateWithActions(
        Action? onDataMissing = null,
        Action? onBootstrapComplete = null,
        Action? onLeanCompleted = null,
        Action? onLeanFailed = null)
    {
        return new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            // === From Idle ===
            .AddTransition(
                BacktestState.Idle, 
                BacktestEvent.SelectSession, 
                BacktestState.SessionSelected)
            .AddTransition(
                BacktestState.Idle, 
                BacktestEvent.CreateSession, 
                BacktestState.SessionSelected)
            
            // === From SessionSelected ===
            .AddTransition(
                BacktestState.SessionSelected, 
                BacktestEvent.CheckData, 
                BacktestState.DataChecking)
            .AddTransition(
                BacktestState.SessionSelected,
                BacktestEvent.Reset,
                BacktestState.Idle)
            
            // === From DataChecking ===
            .AddTransition(
                BacktestState.DataChecking, 
                BacktestEvent.DataMissing, 
                BacktestState.DataBootstrapping,
                action: onDataMissing)
            .AddTransition(
                BacktestState.DataChecking, 
                BacktestEvent.DataReady, 
                BacktestState.ExecutingLean)
            
            // === From DataBootstrapping ===
            .AddTransition(
                BacktestState.DataBootstrapping, 
                BacktestEvent.BootstrapComplete, 
                BacktestState.DataChecking,
                action: onBootstrapComplete)
            .AddTransition(
                BacktestState.DataBootstrapping, 
                BacktestEvent.BootstrapFailed, 
                BacktestState.Failed)
            
            // === From ExecutingLean ===
            .AddTransition(
                BacktestState.ExecutingLean, 
                BacktestEvent.LeanCompleted, 
                BacktestState.Completed,
                action: onLeanCompleted)
            .AddTransition(
                BacktestState.ExecutingLean, 
                BacktestEvent.LeanFailed, 
                BacktestState.Failed,
                action: onLeanFailed)
            
            // === Terminal State Reset Paths ===
            .AddTransition(
                BacktestState.Completed, 
                BacktestEvent.Reset, 
                BacktestState.Idle)
            .AddTransition(
                BacktestState.Failed, 
                BacktestEvent.Reset, 
                BacktestState.Idle)
            
            // === Terminal States ===
            .SetTerminal(BacktestState.Completed)
            .SetTerminal(BacktestState.Failed);
    }
}
