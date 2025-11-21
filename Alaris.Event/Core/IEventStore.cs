namespace Alaris.Event.Core;

/// <summary>
/// Interface for storing and retrieving events.
/// Implementations must ensure events are never modified or deleted.
/// </summary>
/// <remarks>
/// Rule 17 (Audibility): Event stores must be append-only.
/// Events can only be added, never updated or removed.
/// </remarks>
public interface IEventStore
{
    /// <summary>
    /// Appends an event to the event store.
    /// </summary>
    /// <param name="event">The domain event to store.</param>
    /// <param name="aggregateId">Optional aggregate identifier.</param>
    /// <param name="aggregateType">Optional aggregate type.</param>
    /// <param name="initiatedBy">The user or system that initiated this event.</param>
    /// <param name="metadata">Additional metadata for the event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored event envelope with sequence number.</returns>
    Task<EventEnvelope> AppendAsync<TEvent>(
        TEvent @event,
        string? aggregateId = null,
        string? aggregateType = null,
        string? initiatedBy = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where TEvent : IEvent;

    /// <summary>
    /// Retrieves all events for a specific aggregate.
    /// </summary>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All events for the aggregate in chronological order.</returns>
    Task<IReadOnlyList<EventEnvelope>> GetEventsForAggregateAsync(
        string aggregateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves events starting from a specific sequence number.
    /// Used for event replay and projections.
    /// </summary>
    /// <param name="fromSequenceNumber">The sequence number to start from (inclusive).</param>
    /// <param name="maxCount">Maximum number of events to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Events in chronological order.</returns>
    Task<IReadOnlyList<EventEnvelope>> GetEventsFromSequenceAsync(
        long fromSequenceNumber,
        int maxCount = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current highest sequence number in the event store.
    /// </summary>
    Task<long> GetCurrentSequenceNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves events by correlation ID for tracing.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All events with the given correlation ID.</returns>
    Task<IReadOnlyList<EventEnvelope>> GetEventsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves events within a time range.
    /// </summary>
    /// <param name="fromUtc">Start time (inclusive).</param>
    /// <param name="toUtc">End time (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Events within the time range.</returns>
    Task<IReadOnlyList<EventEnvelope>> GetEventsByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
