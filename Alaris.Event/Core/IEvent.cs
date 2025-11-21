namespace Alaris.Event.Core;

/// <summary>
/// Base interface for all domain events in the system.
/// Events are immutable records of things that have happened.
/// </summary>
/// <remarks>
/// Rule 17 (Audibility): All events must be immutable and never deleted.
/// Events represent facts that have occurred and cannot be changed.
/// </remarks>
public interface IEvent
{
    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the UTC timestamp when this event occurred.
    /// </summary>
    DateTime OccurredAtUtc { get; }

    /// <summary>
    /// Gets the type name of this event for serialization/routing.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Gets the correlation ID for tracing related events across boundaries.
    /// </summary>
    string? CorrelationId { get; }
}
