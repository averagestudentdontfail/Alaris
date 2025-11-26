using Alaris.Events.Core;
using System.Collections.Concurrent;

namespace Alaris.Events.Infrastructure;

/// <summary>
/// In-memory implementation of EVCR002A for development and testing.
/// NOT suitable for production use - events are lost on process restart.
/// </summary>
/// <remarks>
/// For production, implement a persistent store using:
/// - SQL database (PostgreSQL, SQL Server)
/// - Event store database (EventStoreDB)
/// - NoSQL database (MongoDB, CosmosDB)
/// </remarks>
public sealed class EVIF001A : EVCR002A
{
    private readonly ConcurrentDictionary<long, EVCR003A> _events = new();
    private long _currentSequence;
    private readonly object _lock = new();

    public Task<EVCR003A> AppendAsync<TEvent>(
        TEvent domainEvent,
        string? aggregateId = null,
        string? aggregateType = null,
        string? initiatedBy = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where TEvent : EVCR001A
    {
        long sequenceNumber;
        lock (_lock)
        {
            sequenceNumber = ++_currentSequence;
        }

        EVCR003A envelope = EVCR003A.Create(
            domainEvent,
            sequenceNumber,
            aggregateId,
            aggregateType,
            initiatedBy,
            metadata);

        _events[sequenceNumber] = envelope;

        return Task.FromResult(envelope);
    }

    public Task<IReadOnlyList<EVCR003A>> GetEventsForAggregateAsync(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EVCR003A> events = _events.Values
            .Where(e => e.AggregateId == aggregateId)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        return Task.FromResult(events);
    }

    public Task<IReadOnlyList<EVCR003A>> GetEventsFromSequenceAsync(
        long fromSequenceNumber,
        int maxCount = 1000,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EVCR003A> events = _events.Values
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

    public Task<IReadOnlyList<EVCR003A>> GetEventsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EVCR003A> events = _events.Values
            .Where(e => e.CorrelationId == correlationId)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        return Task.FromResult(events);
    }

    public Task<IReadOnlyList<EVCR003A>> GetEventsByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EVCR003A> events = _events.Values
            .Where(e => e.StoredAtUtc >= fromUtc && e.StoredAtUtc <= toUtc)
            .OrderBy(e => e.SequenceNumber)
            .ToList();

        return Task.FromResult(events);
    }
}
