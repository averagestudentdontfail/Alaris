// PLWF001A.cs - Finite State Machine engine for workflow routing
// Production-optimized implementation with formal automata theory guarantees

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Alaris.Infrastructure.Protocol.Workflow;

/// <summary>
/// Finite State Machine engine for deterministic workflow routing.
/// Component ID: PLWF001A
/// </summary>
/// <remarks>
/// <para>
/// Implements a DFA M = (Σ, S, s₀, δ, F) where:
/// <list type="bullet">
///   <item>Σ = Input alphabet (TEvent enum)</item>
///   <item>S = Finite set of states (TState enum)</item>
///   <item>s₀ = Initial state</item>
///   <item>δ: S × Σ → S = Partial transition function</item>
///   <item>F ⊆ S = Terminal (accepting) states</item>
/// </list>
/// </para>
/// <para>
/// Production guarantees:
/// <list type="bullet">
///   <item>Determinism: Each (state, event) pair maps to exactly one transition</item>
///   <item>Totality: Invalid transitions return explicit errors (partial function)</item>
///   <item>Thread-safety: Transitions are atomic via SpinLock</item>
///   <item>Memory-bounded: Circular history buffer with configurable capacity</item>
///   <item>Immutability: FSM definition frozen after first Fire()</item>
///   <item>Moore actions: State entry/exit actions for clean separation</item>
/// </list>
/// </para>
/// <para>
/// Reference: Hopcroft, Motwani, Ullman (2006) "Introduction to Automata Theory"
/// </para>
/// </remarks>
/// <typeparam name="TState">State enumeration type</typeparam>
/// <typeparam name="TEvent">Event enumeration type</typeparam>
public sealed class PLWF001A<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    // === Core DFA components ===
    private readonly Dictionary<(TState, TEvent), Transition> _transitions = new Dictionary<(TState, TEvent), Transition>();
    private readonly HashSet<TState> _terminalStates = new HashSet<TState>();
    private readonly TState _initialState;
    
    // === Moore model extensions: state-based actions ===
    private readonly Dictionary<TState, Action?> _stateEntryActions = new Dictionary<TState, Action?>();
    private readonly Dictionary<TState, Action?> _stateExitActions = new Dictionary<TState, Action?>();
    
    // === Thread safety ===
    private SpinLock _stateLock = new(enableThreadOwnerTracking: false);
    private TState _currentState;
    
    // === History with circular buffer for memory bounds ===
    private readonly TransitionRecord[] _historyBuffer;
    private int _historyHead;
    private int _historyCount;
    private readonly object _historyLock = new object();
    
    // === Immutability after first use ===
    private volatile bool _frozen;
    
    /// <summary>
    /// Default history buffer capacity.
    /// </summary>
    public const int DefaultHistoryCapacity = 1000;
    
    /// <summary>
    /// Event raised when a state transition occurs (thread-safe).
    /// </summary>
    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action<T> is simpler for this use case")]
    public event Action<TransitionRecord>? OnTransition;
    
    /// <summary>
    /// Creates a new FSM with the specified initial state and default history capacity.
    /// </summary>
    /// <param name="initialState">The starting state of the FSM.</param>
    public PLWF001A(TState initialState) : this(initialState, DefaultHistoryCapacity)
    {
    }
    
    /// <summary>
    /// Creates a new FSM with specified initial state and history capacity.
    /// </summary>
    /// <param name="initialState">The starting state of the FSM.</param>
    /// <param name="historyCapacity">Maximum number of transitions to retain in history.</param>
    public PLWF001A(TState initialState, int historyCapacity)
    {
        if (historyCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(historyCapacity), "History capacity must be positive");
            
        _initialState = initialState;
        _currentState = initialState;
        _historyBuffer = new TransitionRecord[historyCapacity];
    }
    
    /// <summary>
    /// Current state of the FSM (thread-safe read).
    /// </summary>
    public TState CurrentState
    {
        get
        {
            bool lockTaken = false;
            try
            {
                _stateLock.Enter(ref lockTaken);
                return _currentState;
            }
            finally
            {
                if (lockTaken) _stateLock.Exit();
            }
        }
    }
    
    /// <summary>
    /// Initial state of the FSM.
    /// </summary>
    public TState InitialState => _initialState;
    
    /// <summary>
    /// Whether the FSM is in a terminal (final/accepting) state.
    /// </summary>
    public bool IsTerminal => _terminalStates.Contains(CurrentState);
    
    /// <summary>
    /// Whether the FSM definition is frozen (no more modifications allowed).
    /// </summary>
    public bool IsFrozen => _frozen;
    
    /// <summary>
    /// Number of transitions in history (may be less than total if buffer wrapped).
    /// </summary>
    public int HistoryCount
    {
        get
        {
            lock (_historyLock)
            {
                return _historyCount;
            }
        }
    }
    
    /// <summary>
    /// Maximum history capacity.
    /// </summary>
    public int HistoryCapacity => _historyBuffer.Length;
    
    /// <summary>
    /// History of transitions (most recent first, bounded by capacity).
    /// </summary>
    public IReadOnlyList<TransitionRecord> History
    {
        get
        {
            lock (_historyLock)
            {
                int count = Math.Min(_historyCount, _historyBuffer.Length);
                TransitionRecord[] result = new TransitionRecord[count];
                
                for (int i = 0; i < count; i++)
                {
                    // Read from newest to oldest
                    int index = (_historyHead - 1 - i + _historyBuffer.Length) % _historyBuffer.Length;
                    result[i] = _historyBuffer[index];
                }
                
                return result;
            }
        }
    }
    
    /// <summary>
    /// Defines a transition from one state to another on a given event.
    /// </summary>
    /// <param name="from">Source state</param>
    /// <param name="on">Triggering event</param>
    /// <param name="to">Target state</param>
    /// <param name="guard">Optional guard condition (must return true for transition to execute)</param>
    /// <param name="action">Optional action to execute on successful transition (Mealy-style)</param>
    /// <returns>This FSM instance for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">If FSM is frozen or transition already exists.</exception>
    public PLWF001A<TState, TEvent> AddTransition(
        TState from, 
        TEvent on, 
        TState to,
        Func<bool>? guard = null,
        Action? action = null)
    {
        ThrowIfFrozen();
        
        (TState, TEvent) key = (from, on);
        if (_transitions.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Transition already defined: {from} --[{on}]--> (duplicate). DFA requires determinism.");
        }
        
        _transitions[key] = new Transition(to, guard, action);
        return this;
    }
    
    /// <summary>
    /// Sets Moore-style entry action for a state (executed when entering state).
    /// </summary>
    /// <param name="state">The state to attach the action to.</param>
    /// <param name="entryAction">Action to execute when entering this state.</param>
    /// <returns>This FSM instance for fluent chaining.</returns>
    public PLWF001A<TState, TEvent> OnEntry(TState state, Action entryAction)
    {
        ThrowIfFrozen();
        _stateEntryActions[state] = entryAction;
        return this;
    }
    
    /// <summary>
    /// Sets Moore-style exit action for a state (executed when leaving state).
    /// </summary>
    /// <param name="state">The state to attach the action to.</param>
    /// <param name="exitAction">Action to execute when exiting this state.</param>
    /// <returns>This FSM instance for fluent chaining.</returns>
    public PLWF001A<TState, TEvent> OnExit(TState state, Action exitAction)
    {
        ThrowIfFrozen();
        _stateExitActions[state] = exitAction;
        return this;
    }
    
    /// <summary>
    /// Marks a state as terminal (accepting/final).
    /// </summary>
    /// <param name="state">The state to mark as terminal.</param>
    /// <returns>This FSM instance for fluent chaining.</returns>
    public PLWF001A<TState, TEvent> SetTerminal(TState state)
    {
        ThrowIfFrozen();
        _terminalStates.Add(state);
        return this;
    }
    
    /// <summary>
    /// Freezes the FSM definition, preventing further modifications.
    /// Called automatically on first Fire(), but can be called explicitly.
    /// </summary>
    /// <returns>This FSM instance for fluent chaining.</returns>
    public PLWF001A<TState, TEvent> Freeze()
    {
        _frozen = true;
        return this;
    }
    
    /// <summary>
    /// Attempts to fire an event, transitioning the FSM if valid.
    /// Thread-safe via SpinLock for atomic state transitions.
    /// </summary>
    /// <param name="event">The event to fire</param>
    /// <returns>Transition result indicating success or failure reason</returns>
    public TransitionResult<TState, TEvent> Fire(TEvent @event)
    {
        // Freeze on first use (defensive immutability)
        _frozen = true;
        
        bool lockTaken = false;
        try
        {
            _stateLock.Enter(ref lockTaken);
            
            (TState, TEvent) key = (_currentState, @event);
            
            if (!_transitions.TryGetValue(key, out Transition transition))
            {
                TransitionResult<TState, TEvent> result = TransitionResult<TState, TEvent>.InvalidTransition(_currentState, @event);
                RecordTransition(result);
                return result;
            }
            
            // Check guard condition
            if (transition.Guard != null && !transition.Guard())
            {
                TransitionResult<TState, TEvent> result = TransitionResult<TState, TEvent>.GuardFailed(_currentState, @event, transition.Target);
                RecordTransition(result);
                return result;
            }
            
            TState previousState = _currentState;
            
            // Execute exit action (Moore model)
            try
            {
                if (_stateExitActions.TryGetValue(previousState, out Action? exitAction))
                    exitAction?.Invoke();
            }
            catch (Exception ex)
            {
                TransitionResult<TState, TEvent> result = TransitionResult<TState, TEvent>.ActionFailed(previousState, @event, ex, "Exit action failed");
                RecordTransition(result);
                return result;
            }
            
            // Execute transition action (Mealy model)
            try
            {
                transition.Action?.Invoke();
            }
            catch (Exception ex)
            {
                TransitionResult<TState, TEvent> result = TransitionResult<TState, TEvent>.ActionFailed(_currentState, @event, ex, "Transition action failed");
                RecordTransition(result);
                return result;
            }
            
            // Transition to new state
            _currentState = transition.Target;
            
            // Execute entry action (Moore model)
            try
            {
                if (_stateEntryActions.TryGetValue(_currentState, out Action? entryAction))
                    entryAction?.Invoke();
            }
            catch (Exception ex)
            {
                // State already changed, log but don't revert
                TransitionResult<TState, TEvent> result = TransitionResult<TState, TEvent>.ActionFailed(previousState, @event, ex, "Entry action failed");
                RecordTransition(result);
                return result;
            }
            
            TransitionResult<TState, TEvent> successResult = TransitionResult<TState, TEvent>.Success(previousState, @event, _currentState);
            RecordTransition(successResult);
            return successResult;
        }
        finally
        {
            if (lockTaken) _stateLock.Exit();
        }
    }
    
    /// <summary>
    /// Checks if a transition is possible without executing it (thread-safe).
    /// </summary>
    /// <param name="event">The event to check.</param>
    /// <returns>True if the transition can be fired from the current state.</returns>
    public bool CanFire(TEvent @event)
    {
        bool lockTaken = false;
        try
        {
            _stateLock.Enter(ref lockTaken);
            
            (TState, TEvent) key = (_currentState, @event);
            
            if (!_transitions.TryGetValue(key, out Transition transition))
                return false;
                
            return transition.Guard == null || transition.Guard();
        }
        finally
        {
            if (lockTaken) _stateLock.Exit();
        }
    }
    
    /// <summary>
    /// Gets all events that can be fired from the current state.
    /// Takes guard conditions into account.
    /// </summary>
    /// <returns>Enumerable of available events.</returns>
    public IEnumerable<TEvent> GetAvailableEvents()
    {
        TState currentState = CurrentState;
        List<TEvent> events = new List<TEvent>();
        foreach (KeyValuePair<(TState, TEvent), Transition> transition in _transitions)
        {
            if (!transition.Key.Item1.Equals(currentState))
            {
                continue;
            }

            TEvent candidate = transition.Key.Item2;
            if (CanFire(candidate))
            {
                events.Add(candidate);
            }
        }

        return events;
    }
    
    /// <summary>
    /// Gets all defined transitions from the current state (ignoring guards).
    /// </summary>
    /// <returns>Enumerable of (event, targetState) pairs.</returns>
    public IEnumerable<(TEvent Event, TState Target)> GetPossibleTransitions()
    {
        TState currentState = CurrentState;
        List<(TEvent Event, TState Target)> transitions = new List<(TEvent Event, TState Target)>();
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            if (kvp.Key.Item1.Equals(currentState))
            {
                transitions.Add((kvp.Key.Item2, kvp.Value.Target));
            }
        }

        return transitions;
    }
    
    /// <summary>
    /// Resets the FSM to its initial state (thread-safe).
    /// Clears history buffer.
    /// </summary>
    public void Reset()
    {
        bool lockTaken = false;
        try
        {
            _stateLock.Enter(ref lockTaken);
            _currentState = _initialState;
        }
        finally
        {
            if (lockTaken) _stateLock.Exit();
        }
        
        lock (_historyLock)
        {
            _historyHead = 0;
            _historyCount = 0;
            Array.Clear(_historyBuffer, 0, _historyBuffer.Length);
        }
    }
    
    /// <summary>
    /// Validates the FSM definition for completeness and correctness.
    /// </summary>
    /// <returns>List of validation issues (empty if valid).</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> issues = new List<string>();
        
        // Check that initial state has at least one outgoing transition
        bool hasInitialTransition = false;
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            if (kvp.Key.Item1.Equals(_initialState))
            {
                hasInitialTransition = true;
                break;
            }
        }

        if (!hasInitialTransition)
        {
            issues.Add($"Initial state '{_initialState}' has no outgoing transitions");
        }
        
        // Build reachability graph
        HashSet<TState> allStates = new HashSet<TState>();
        allStates.Add(_initialState);
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            allStates.Add(kvp.Key.Item1);
            allStates.Add(kvp.Value.Target);
        }
            
        HashSet<TState> reachableStates = new HashSet<TState> { _initialState };
        Queue<TState> frontier = new Queue<TState>();
        frontier.Enqueue(_initialState);
        
        while (frontier.Count > 0)
        {
            TState current = frontier.Dequeue();
            foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
            {
                if (!kvp.Key.Item1.Equals(current))
                {
                    continue;
                }

                TState target = kvp.Value.Target;
                if (reachableStates.Add(target))
                {
                    frontier.Enqueue(target);
                }
            }
        }
        
        // Check for unreachable states
        List<TState> unreachable = new List<TState>();
        foreach (TState state in allStates)
        {
            if (!reachableStates.Contains(state))
            {
                unreachable.Add(state);
            }
        }

        foreach (TState state in unreachable)
        {
            issues.Add($"State '{state}' is unreachable from initial state");
        }
        
        // Check for dead states (non-terminal states with no outgoing transitions)
        HashSet<TState> statesWithOutgoing = new HashSet<TState>();
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            statesWithOutgoing.Add(kvp.Key.Item1);
        }

        foreach (TState state in allStates)
        {
            if (_terminalStates.Contains(state))
                continue;
                
            if (!statesWithOutgoing.Contains(state))
            {
                issues.Add($"Non-terminal state '{state}' has no outgoing transitions (dead state)");
            }
        }
        
        // Check that all terminal states are reachable
        foreach (TState terminal in _terminalStates)
        {
            if (!reachableStates.Contains(terminal))
            {
                issues.Add($"Terminal state '{terminal}' is unreachable");
            }
        }
        
        return issues;
    }
    
    /// <summary>
    /// Gets statistics about the FSM structure.
    /// </summary>
    public FsmStatistics GetStatistics()
    {
        HashSet<TState> allStates = new HashSet<TState>();
        HashSet<TEvent> allEvents = new HashSet<TEvent>();
        allStates.Add(_initialState);
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            allStates.Add(kvp.Key.Item1);
            allStates.Add(kvp.Value.Target);
            allEvents.Add(kvp.Key.Item2);
        }
        
        return new FsmStatistics(
            StateCount: allStates.Count,
            EventCount: allEvents.Count,
            TransitionCount: _transitions.Count,
            TerminalStateCount: _terminalStates.Count,
            TransitionDensity: allStates.Count > 0 && allEvents.Count > 0 
                ? (double)_transitions.Count / (allStates.Count * allEvents.Count) 
                : 0,
            HasCycles: DetectCycles()
        );
    }
    
    private bool DetectCycles()
    {
        HashSet<TState> visited = new HashSet<TState>();
        HashSet<TState> inStack = new HashSet<TState>();
        
        HashSet<TState> statesWithOutgoing = new HashSet<TState>();
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            statesWithOutgoing.Add(kvp.Key.Item1);
        }

        foreach (TState state in statesWithOutgoing)
        {
            if (DetectCyclesDfs(state, visited, inStack))
                return true;
        }
        
        return false;
    }
    
    private bool DetectCyclesDfs(TState state, HashSet<TState> visited, HashSet<TState> inStack)
    {
        if (inStack.Contains(state))
            return true;
            
        if (visited.Contains(state))
            return false;
            
        visited.Add(state);
        inStack.Add(state);
        
        foreach (KeyValuePair<(TState, TEvent), Transition> kvp in _transitions)
        {
            if (!kvp.Key.Item1.Equals(state))
            {
                continue;
            }

            TState target = kvp.Value.Target;
            if (DetectCyclesDfs(target, visited, inStack))
                return true;
        }
        
        inStack.Remove(state);
        return false;
    }
    
    private void RecordTransition(TransitionResult<TState, TEvent> result)
    {
        TransitionRecord record = new TransitionRecord(
            result.FromState?.ToString() ?? "",
            result.Event?.ToString() ?? "",
            result.ToState?.ToString() ?? "",
            result.Succeeded,
            DateTime.UtcNow,
            result.Message
        );
        
        lock (_historyLock)
        {
            _historyBuffer[_historyHead] = record;
            _historyHead = (_historyHead + 1) % _historyBuffer.Length;
            _historyCount = Math.Min(_historyCount + 1, _historyBuffer.Length);
        }
        
        // Raise event outside lock to prevent deadlocks
        OnTransition?.Invoke(record);
    }
    
    private void ThrowIfFrozen()
    {
        if (_frozen)
        {
            throw new InvalidOperationException(
                "FSM definition is frozen and cannot be modified. " +
                "All transitions must be defined before the first Fire() call.");
        }
    }
    
    private readonly record struct Transition(
        TState Target,
        Func<bool>? Guard,
        Action? Action);
}

/// <summary>
/// Result of a state transition attempt.
/// </summary>
/// <typeparam name="TState">State type.</typeparam>
/// <typeparam name="TEvent">Event type.</typeparam>
public readonly record struct TransitionResult<TState, TEvent>
    where TState : struct, Enum
    where TEvent : struct, Enum
{
    /// <summary>Whether the transition succeeded.</summary>
    public bool Succeeded { get; init; }
    
    /// <summary>Source state (before transition).</summary>
    public TState? FromState { get; init; }
    
    /// <summary>Event that triggered the transition.</summary>
    public TEvent? Event { get; init; }
    
    /// <summary>Target state (after transition, if successful).</summary>
    public TState? ToState { get; init; }
    
    /// <summary>Human-readable message describing the result.</summary>
    public string Message { get; init; }
    
    /// <summary>Exception if action failed.</summary>
    public Exception? Exception { get; init; }
    
    /// <summary>Creates a successful transition result.</summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern")]
    public static TransitionResult<TState, TEvent> Success(TState from, TEvent on, TState to)
        => new TransitionResult<TState, TEvent> 
        { 
            Succeeded = true, 
            FromState = from,
            Event = on,
            ToState = to,
            Message = $"{from} --[{on}]--> {to}" 
        };
        
    /// <summary>Creates an invalid transition result (no transition defined).</summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern")]
    public static TransitionResult<TState, TEvent> InvalidTransition(TState from, TEvent on)
        => new TransitionResult<TState, TEvent> 
        { 
            Succeeded = false, 
            FromState = from,
            Event = on,
            Message = $"No transition from '{from}' on event '{on}'" 
        };
        
    /// <summary>Creates a guard-failed result.</summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern")]
    public static TransitionResult<TState, TEvent> GuardFailed(TState from, TEvent on, TState to)
        => new TransitionResult<TState, TEvent> 
        { 
            Succeeded = false, 
            FromState = from,
            Event = on,
            ToState = to,
            Message = $"Guard failed: {from} --[{on}]--> {to}" 
        };
        
    /// <summary>Creates an action-failed result.</summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory method pattern")]
    public static TransitionResult<TState, TEvent> ActionFailed(TState from, TEvent on, Exception ex, string? context = null)
        => new TransitionResult<TState, TEvent> 
        { 
            Succeeded = false, 
            FromState = from,
            Event = on,
            Message = context != null 
                ? $"{context}: {ex.Message}" 
                : $"Action failed on event '{on}': {ex.Message}",
            Exception = ex 
        };
}

/// <summary>
/// Record of a single state transition for audit/debugging.
/// </summary>
/// <param name="FromState">State before transition.</param>
/// <param name="Event">Event that triggered transition.</param>
/// <param name="ToState">State after transition.</param>
/// <param name="Succeeded">Whether transition succeeded.</param>
/// <param name="Timestamp">When the transition occurred.</param>
/// <param name="Message">Optional message (e.g., error details).</param>
public readonly record struct TransitionRecord(
    string FromState,
    string Event,
    string ToState,
    bool Succeeded,
    DateTime Timestamp,
    string? Message = null
);

/// <summary>
/// Statistics about FSM structure.
/// </summary>
/// <param name="StateCount">Number of unique states.</param>
/// <param name="EventCount">Number of unique events.</param>
/// <param name="TransitionCount">Number of defined transitions.</param>
/// <param name="TerminalStateCount">Number of terminal states.</param>
/// <param name="TransitionDensity">Ratio of defined transitions to possible (0-1).</param>
/// <param name="HasCycles">Whether the FSM contains cycles.</param>
public readonly record struct FsmStatistics(
    int StateCount,
    int EventCount,
    int TransitionCount,
    int TerminalStateCount,
    double TransitionDensity,
    bool HasCycles
);
