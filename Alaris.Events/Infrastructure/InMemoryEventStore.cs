using Alaris.Events.Core;
using System.Collections.Concurrent;

namespace Alaris.Events.Infrastructure;

/// <summary>
/// In-memory implementation of IEventStore for development and testing.
/// NOT suitable for production use - events are lost on process restart.
/// </summary>
/// <remarks>
/// For production, implement a persistent store using:
/// - SQL database (PostgreSQL, SQL Server)
/// - Event store database (EventStoreDB)
/// - NoSQL database (MongoDB, CosmosDB)
/// </remarks>
public sealed class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<long, EventEnvelope> _events = new();
    private long _currentSequence;
    private readonly object _lock = new();

    public Task<EventEnvelope> AppendAsync<TEvent>(
        TEvent domainEvent,
        string? aggregateId = null,
        string? aggregateType = null,
        string? initiatedBy = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where TEvent : IEvent
    {
        long sequenceNumber;
        lock (_lock)
        {
            sequenceNumber = ++_currentSequence;
        }

        EventEnvelope envelope = EventEnvelope.Create(
            domainEvent,
            sequenceNumber,
            aggregateId,
            aggregateType,
            initiatedBy,
            metadata);

        _events[sequenceNumber] = envelope;

        return Task.FromResult(envelope);
    }

    public Task<IReadOnlyList<EventEnvelope>> GetEventsForAggregateAsync(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EventEnvelope> events = _events.Values
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        return Task.FromResult(events);
    }

    public Task<IReadOnlyList<EventEnvelope>> GetEventsFromSequenceAsync(
        long fromSequenceNumber,
        int maxCount = 1000,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EventEnvelope> events = _events.Values
            .Where(e => e.SequenceNumber >= fromSequenceNumber)
            .OrderBy(e => e.SequenceNumber)
            .Take(maxCount)
            .ToList();

        return Task.FromResult(events);
    }

    public Task<long> GetCurrentSequenceNumberAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSequence);
    }

    public Task<IReadOnlyList<EventEnvelope>> GetEventsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EventEnvelope> events = _events.Values
            .Where(e => e.CorrelationId == correlationId)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        return Task.FromResult(events);
    }

    public Task<IReadOnlyList<EventEnvelope>> GetEventsByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EventEnvelope> events = _events.Values
            .Where(e => e.StoredAtUtc >= fromUtc && e.StoredAtUtc <= toUtc)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        return Task.FromResult(events);
    }
}
