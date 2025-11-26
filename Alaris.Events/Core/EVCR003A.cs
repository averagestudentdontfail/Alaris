namespace Alaris.Events.Core;

/// <summary>
/// Immutable envelope that wraps an event with metadata.
/// This ensures events can never be modified after creation.
/// </summary>
/// <remarks>
/// Rule 17 (Audibility): EventEnvelopes are immutable by design.
/// Once created, they represent an unchangeable record of what happened.
/// </remarks>
public sealed record EVCR003A
{
    /// <summary>
    /// Gets the unique identifier for this event envelope.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Gets the sequence number of this event in the event stream.
    /// Monotonically increasing, never reused.
    /// </summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this event was stored.
    /// </summary>
    public required DateTime StoredAtUtc { get; init; }

    /// <summary>
    /// Gets the type name of the event for deserialization.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Gets the serialized event data (JSON).
    /// </summary>
    public required string EventData { get; init; }

    /// <summary>
    /// Gets the aggregate ID this event belongs to (if applicable).
    /// </summary>
    public string? AggregateId { get; init; }

    /// <summary>
    /// Gets the aggregate type this event belongs to (if applicable).
    /// </summary>
    public string? AggregateType { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracing related events.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the causation ID (the event that caused this event).
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// Gets the user or system that initiated this event.
    /// </summary>
    public string? InitiatedBy { get; init; }

    /// <summary>
    /// Gets additional metadata as key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Creates an EVCR003A from a domain event.
    /// </summary>
    public static EVCR003A Create<TEvent>(
        TEvent domainEvent,
        long sequenceNumber,
        string? aggregateId = null,
        string? aggregateType = null,
        string? initiatedBy = null,
        IReadOnlyDictionary<string, string>? metadata = null) where TEvent : EVCR001A
    {
        string eventData = System.Text.Json.JsonSerializer.Serialize(domainEvent);

        return new EVCR003A
        {
            EventId = domainEvent.EventId,
            SequenceNumber = sequenceNumber,
            StoredAtUtc = DateTime.UtcNow,
            EventType = domainEvent.EventType,
            EventData = eventData,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            CorrelationId = domainEvent.CorrelationId,
            CausationId = null, // Can be set externally if needed
            InitiatedBy = initiatedBy,
            Metadata = metadata
        };
    }
}
