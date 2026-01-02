// TSUN051A.cs - Unit tests for FSM workflow engine (PLWF001A)

using Alaris.Infrastructure.Protocol.Workflow;
using Xunit;

namespace Alaris.Test.Unit;

/// <summary>
/// Unit tests for FSM workflow engine.
/// Component ID: TSUN051A
/// </summary>
/// <remarks>
/// Tests validate:
/// - Determinism: Each (state, event) → exactly one transition
/// - Guard enforcement: Transitions blocked when guards fail
/// - Action execution: Side-effects run on successful transitions
/// - Terminal states: FSM correctly identifies terminal states
/// - Validation: FSM detects structural issues
/// </remarks>
public class TSUN051A
{
    #region FSM Engine Core Tests

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle);
        
        Assert.Equal(BacktestState.Idle, fsm.CurrentState);
        Assert.Equal(BacktestState.Idle, fsm.InitialState);
    }

    [Fact]
    public void AddTransition_ValidTransition_Succeeds()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle);
        
        var result = fsm.AddTransition(
            BacktestState.Idle,
            BacktestEvent.SelectSession,
            BacktestState.SessionSelected);
        
        Assert.Same(fsm, result); // Fluent API returns same instance
    }

    [Fact]
    public void AddTransition_Duplicate_ThrowsInvalidOperationException()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle);
        
        fsm.AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            fsm.AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.DataChecking));
        
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DFA", ex.Message, StringComparison.OrdinalIgnoreCase); // Should mention determinism
    }

    [Fact]
    public void Fire_ValidTransition_ChangesState()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        var result = fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(result.Succeeded);
        Assert.Equal(BacktestState.SessionSelected, fsm.CurrentState);
        Assert.Equal(BacktestState.Idle, result.FromState);
        Assert.Equal(BacktestState.SessionSelected, result.ToState);
        Assert.Equal(BacktestEvent.SelectSession, result.Event);
    }

    [Fact]
    public void Fire_InvalidTransition_ReturnsFailure()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        var result = fsm.Fire(BacktestEvent.StartLean); // Not a valid event from Idle
        
        Assert.False(result.Succeeded);
        Assert.Equal(BacktestState.Idle, fsm.CurrentState); // State unchanged
        Assert.Contains("No transition", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Guard Condition Tests

    [Fact]
    public void Fire_GuardPasses_TransitionsSuccessfully()
    {
        var dataReady = true;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(
                BacktestState.Idle,
                BacktestEvent.SelectSession,
                BacktestState.SessionSelected,
                guard: () => dataReady);
        
        var result = fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(result.Succeeded);
        Assert.Equal(BacktestState.SessionSelected, fsm.CurrentState);
    }

    [Fact]
    public void Fire_GuardFails_TransitionBlocked()
    {
        var dataReady = false;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(
                BacktestState.Idle,
                BacktestEvent.SelectSession,
                BacktestState.SessionSelected,
                guard: () => dataReady);
        
        var result = fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.False(result.Succeeded);
        Assert.Equal(BacktestState.Idle, fsm.CurrentState); // State unchanged
        Assert.Contains("Guard failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanFire_GuardFails_ReturnsFalse()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(
                BacktestState.Idle,
                BacktestEvent.SelectSession,
                BacktestState.SessionSelected,
                guard: () => false);
        
        Assert.False(fsm.CanFire(BacktestEvent.SelectSession));
    }

    [Fact]
    public void CanFire_NoGuard_ReturnsTrue()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        Assert.True(fsm.CanFire(BacktestEvent.SelectSession));
    }

    #endregion

    #region Action Execution Tests

    [Fact]
    public void Fire_WithAction_ExecutesAction()
    {
        var actionExecuted = false;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(
                BacktestState.Idle,
                BacktestEvent.SelectSession,
                BacktestState.SessionSelected,
                action: () => actionExecuted = true);
        
        fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(actionExecuted);
    }

    [Fact]
    public void Fire_ActionThrows_ReturnsFailure()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(
                BacktestState.Idle,
                BacktestEvent.SelectSession,
                BacktestState.SessionSelected,
                action: () => throw new InvalidOperationException("Test error"));
        
        var result = fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.False(result.Succeeded);
        Assert.Equal(BacktestState.Idle, fsm.CurrentState); // State unchanged
        Assert.Contains("Action failed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Exception);
    }

    #endregion

    #region Terminal State Tests

    [Fact]
    public void IsTerminal_InTerminalState_ReturnsTrue()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Completed)
            .SetTerminal(BacktestState.Completed)
            .SetTerminal(BacktestState.Failed);
        
        Assert.True(fsm.IsTerminal);
    }

    [Fact]
    public void IsTerminal_NotInTerminalState_ReturnsFalse()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .SetTerminal(BacktestState.Completed)
            .SetTerminal(BacktestState.Failed);
        
        Assert.False(fsm.IsTerminal);
    }

    #endregion

    #region History and Audit Trail Tests

    [Fact]
    public void Fire_RecordsHistory()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .AddTransition(BacktestState.SessionSelected, BacktestEvent.CheckData, BacktestState.DataChecking);
        
        fsm.Fire(BacktestEvent.SelectSession);
        fsm.Fire(BacktestEvent.CheckData);
        
        Assert.Equal(2, fsm.HistoryCount);
        // History is now most-recent-first (circular buffer)
        Assert.True(fsm.History[0].Succeeded);
        Assert.Equal("SessionSelected", fsm.History[0].FromState);
        Assert.Equal("DataChecking", fsm.History[0].ToState);
    }

    [Fact]
    public void Fire_FailedTransition_RecordedInHistory()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle);
        
        fsm.Fire(BacktestEvent.StartLean); // Invalid - still records
        
        Assert.Equal(1, fsm.HistoryCount);
        Assert.False(fsm.History[0].Succeeded);
    }

    [Fact]
    public void OnTransition_EventRaised()
    {
        var eventFired = false;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        fsm.OnTransition += record => eventFired = true;
        fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(eventFired);
    }

    [Fact]
    public void History_CircularBuffer_BoundsMemory()
    {
        const int capacity = 5;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle, historyCapacity: capacity)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .AddTransition(BacktestState.SessionSelected, BacktestEvent.Reset, BacktestState.Idle);
        
        // Fire more transitions than capacity
        for (int i = 0; i < 10; i++)
        {
            fsm.Fire(BacktestEvent.SelectSession);
            fsm.Fire(BacktestEvent.Reset);
        }
        
        // History should be bounded by capacity
        Assert.Equal(capacity, fsm.History.Count);
        Assert.Equal(capacity, fsm.HistoryCapacity);
    }

    #endregion

    #region Frozen State Tests

    [Fact]
    public void Fire_FreezesDefinition()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        Assert.False(fsm.IsFrozen);
        
        fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(fsm.IsFrozen);
    }

    [Fact]
    public void AddTransition_AfterFire_ThrowsInvalidOperationException()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        fsm.Fire(BacktestEvent.SelectSession);
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            fsm.AddTransition(BacktestState.SessionSelected, BacktestEvent.CheckData, BacktestState.DataChecking));
        
        Assert.Contains("frozen", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Freeze_ExplicitlyFreezesDefinition()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .Freeze();
        
        Assert.True(fsm.IsFrozen);
    }

    #endregion

    #region Moore Model Tests

    [Fact]
    public void OnEntry_ExecutesWhenEnteringState()
    {
        var entryExecuted = false;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .OnEntry(BacktestState.SessionSelected, () => entryExecuted = true);
        
        fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(entryExecuted);
    }

    [Fact]
    public void OnExit_ExecutesWhenLeavingState()
    {
        var exitExecuted = false;
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .OnExit(BacktestState.Idle, () => exitExecuted = true);
        
        fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.True(exitExecuted);
    }

    [Fact]
    public void MooreActions_ExecuteInOrder()
    {
        var order = new List<string>();
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(
                BacktestState.Idle,
                BacktestEvent.SelectSession,
                BacktestState.SessionSelected,
                action: () => order.Add("transition"))
            .OnExit(BacktestState.Idle, () => order.Add("exit"))
            .OnEntry(BacktestState.SessionSelected, () => order.Add("entry"));
        
        fsm.Fire(BacktestEvent.SelectSession);
        
        Assert.Equal(3, order.Count);
        Assert.Equal("exit", order[0]);      // Exit first
        Assert.Equal("transition", order[1]); // Then transition (Mealy)
        Assert.Equal("entry", order[2]);      // Then entry
    }

    #endregion

    #region Statistics and Validation Tests

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        var fsm = PLWF002A.Create();
        
        var stats = fsm.GetStatistics();
        
        Assert.Equal(7, stats.StateCount);  // 7 states used in transitions
        Assert.Equal(12, stats.TransitionCount);  // 12 transitions
        Assert.Equal(2, stats.TerminalStateCount);  // Completed, Failed
        Assert.True(stats.HasCycles);  // DataBootstrapping → DataChecking cycle
        Assert.True(stats.TransitionDensity > 0);
    }

    [Fact]
    public void Validate_DetectsDeadStates()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        // SessionSelected has no outgoing transitions and is not terminal = dead state
        
        var issues = fsm.Validate();
        
        Assert.Contains(issues, i => i.Contains("dead state", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Available Events Tests

    [Fact]
    public void GetAvailableEvents_ReturnsValidEvents()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .AddTransition(BacktestState.Idle, BacktestEvent.CreateSession, BacktestState.SessionSelected)
            .AddTransition(BacktestState.SessionSelected, BacktestEvent.CheckData, BacktestState.DataChecking);
        
        var available = fsm.GetAvailableEvents().ToList();
        
        Assert.Equal(2, available.Count);
        Assert.Contains(BacktestEvent.SelectSession, available);
        Assert.Contains(BacktestEvent.CreateSession, available);
    }

    [Fact]
    public void GetPossibleTransitions_ReturnsAllDefinedTransitions()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected)
            .AddTransition(BacktestState.Idle, BacktestEvent.CreateSession, BacktestState.SessionSelected);
        
        var transitions = fsm.GetPossibleTransitions().ToList();
        
        Assert.Equal(2, transitions.Count);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ReturnsToInitialState()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        fsm.Fire(BacktestEvent.SelectSession);
        Assert.Equal(BacktestState.SessionSelected, fsm.CurrentState);
        
        fsm.Reset();
        
        Assert.Equal(BacktestState.Idle, fsm.CurrentState);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle)
            .AddTransition(BacktestState.Idle, BacktestEvent.SelectSession, BacktestState.SessionSelected);
        
        fsm.Fire(BacktestEvent.SelectSession);
        Assert.Equal(1, fsm.HistoryCount);
        
        fsm.Reset();
        
        Assert.Equal(0, fsm.HistoryCount);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Validate_NoTransitionsFromInitial_ReportsIssue()
    {
        var fsm = new PLWF001A<BacktestState, BacktestEvent>(BacktestState.Idle);
        
        var issues = fsm.Validate();
        
        // Empty FSM has no outgoing transitions AND initial is a dead state
        Assert.True(issues.Count >= 1);
        Assert.Contains(issues, i => i.Contains("no outgoing transitions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ValidFSM_ReturnsEmpty()
    {
        var fsm = PLWF002A.Create();
        
        var issues = fsm.Validate();
        
        Assert.Empty(issues);
    }

    #endregion

    #region Backtest Workflow Integration Tests

    [Fact]
    public void BacktestWorkflow_HappyPath_ReachesCompleted()
    {
        var fsm = PLWF002A.Create();
        
        Assert.Equal(BacktestState.Idle, fsm.CurrentState);
        
        fsm.Fire(BacktestEvent.SelectSession);
        Assert.Equal(BacktestState.SessionSelected, fsm.CurrentState);
        
        fsm.Fire(BacktestEvent.CheckData);
        Assert.Equal(BacktestState.DataChecking, fsm.CurrentState);
        
        fsm.Fire(BacktestEvent.DataReady);
        Assert.Equal(BacktestState.ExecutingLean, fsm.CurrentState);
        
        fsm.Fire(BacktestEvent.LeanCompleted);
        Assert.Equal(BacktestState.Completed, fsm.CurrentState);
        
        Assert.True(fsm.IsTerminal);
    }

    [Fact]
    public void BacktestWorkflow_DataMissing_BootstrapsAndRetries()
    {
        var fsm = PLWF002A.Create();
        
        fsm.Fire(BacktestEvent.SelectSession);
        fsm.Fire(BacktestEvent.CheckData);
        fsm.Fire(BacktestEvent.DataMissing);
        
        Assert.Equal(BacktestState.DataBootstrapping, fsm.CurrentState);
        
        fsm.Fire(BacktestEvent.BootstrapComplete);
        
        Assert.Equal(BacktestState.DataChecking, fsm.CurrentState); // Back to checking
    }

    [Fact]
    public void BacktestWorkflow_BootstrapFails_ReachesFailed()
    {
        var fsm = PLWF002A.Create();
        
        fsm.Fire(BacktestEvent.SelectSession);
        fsm.Fire(BacktestEvent.CheckData);
        fsm.Fire(BacktestEvent.DataMissing);
        fsm.Fire(BacktestEvent.BootstrapFailed);
        
        Assert.Equal(BacktestState.Failed, fsm.CurrentState);
        Assert.True(fsm.IsTerminal);
    }

    [Fact]
    public void BacktestWorkflow_Reset_ReturnsToIdle()
    {
        var fsm = PLWF002A.Create();
        
        fsm.Fire(BacktestEvent.SelectSession);
        fsm.Fire(BacktestEvent.CheckData);
        fsm.Fire(BacktestEvent.DataReady);
        fsm.Fire(BacktestEvent.LeanFailed);
        
        Assert.Equal(BacktestState.Failed, fsm.CurrentState);
        
        fsm.Fire(BacktestEvent.Reset);
        
        Assert.Equal(BacktestState.Idle, fsm.CurrentState);
        Assert.False(fsm.IsTerminal);
    }

    #endregion

    #region Trading Workflow Tests

    [Fact]
    public void TradingWorkflow_Connect_Evaluate_Execute()
    {
        var fsm = PLWF003A.Create();
        
        Assert.Equal(TradingState.Disconnected, fsm.CurrentState);
        
        fsm.Fire(TradingEvent.Connect);
        Assert.Equal(TradingState.Connecting, fsm.CurrentState);
        
        fsm.Fire(TradingEvent.Connected);
        Assert.Equal(TradingState.Ready, fsm.CurrentState);
        
        fsm.Fire(TradingEvent.Evaluate);
        Assert.Equal(TradingState.Evaluating, fsm.CurrentState);
        
        fsm.Fire(TradingEvent.SignalGenerated);
        Assert.Equal(TradingState.Executing, fsm.CurrentState);
        
        fsm.Fire(TradingEvent.TradeExecuted);
        Assert.Equal(TradingState.Monitoring, fsm.CurrentState);
    }

    [Fact]
    public void TradingWorkflow_NoSignal_ReturnsToReady()
    {
        var fsm = PLWF003A.Create();
        
        fsm.Fire(TradingEvent.Connect);
        fsm.Fire(TradingEvent.Connected);
        fsm.Fire(TradingEvent.Evaluate);
        fsm.Fire(TradingEvent.NoSignal);
        
        Assert.Equal(TradingState.Ready, fsm.CurrentState);
    }

    [Fact]
    public void TradingWorkflow_FatalError_ReachesErrorState()
    {
        var fsm = PLWF003A.Create();
        
        fsm.Fire(TradingEvent.Connect);
        fsm.Fire(TradingEvent.Connected);
        fsm.Fire(TradingEvent.Evaluate);
        fsm.Fire(TradingEvent.FatalError);
        
        Assert.Equal(TradingState.Error, fsm.CurrentState);
        Assert.True(fsm.IsTerminal);
    }

    #endregion
}
