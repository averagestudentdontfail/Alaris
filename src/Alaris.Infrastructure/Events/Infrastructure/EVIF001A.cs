using Alaris.Infrastructure.Events.Core;
using System.Collections.Concurrent;

namespace Alaris.Infrastructure.Events.Infrastructure;

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
    private readonly ConcurrentDictionary<long, EVCR003A> _events = new ConcurrentDictionary<long, EVCR003A>();
    private long _currentSequence;
    private readonly object _lock = new object();

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
        List<EVCR003A> events = new List<EVCR003A>();
        foreach (EVCR003A entry in _events.Values)
        {
            if (entry.AggregateId == aggregateId)
            {
                events.Add(entry);
            }
        }

        events.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));

        return Task.FromResult((IReadOnlyList<EVCR003A>)events);
    }

    public Task<IReadOnlyList<EVCR003A>> GetEventsFromSequenceAsync(
        long fromSequenceNumber,
        int maxCount = 1000,
        CancellationToken cancellationToken = default)
    {
        List<EVCR003A> events = new List<EVCR003A>();
        foreach (EVCR003A entry in _events.Values)
        {
            if (entry.SequenceNumber >= fromSequenceNumber)
            {
                events.Add(entry);
            }
        }

        events.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
        if (events.Count > maxCount)
        {
            events.RemoveRange(maxCount, events.Count - maxCount);
        }

        return Task.FromResult((IReadOnlyList<EVCR003A>)events);
    }

    public Task<long> GetCurrentSequenceNumberAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSequence);
    }

    public Task<IReadOnlyList<EVCR003A>> GetEventsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        List<EVCR003A> events = new List<EVCR003A>();
        foreach (EVCR003A entry in _events.Values)
        {
            if (entry.CorrelationId == correlationId)
            {
                events.Add(entry);
            }
        }

        events.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));

        return Task.FromResult((IReadOnlyList<EVCR003A>)events);
    }

    public Task<IReadOnlyList<EVCR003A>> GetEventsByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        List<EVCR003A> events = new List<EVCR003A>();
        foreach (EVCR003A entry in _events.Values)
        {
            if (entry.StoredAtUtc >= fromUtc && entry.StoredAtUtc <= toUtc)
            {
                events.Add(entry);
            }
        }

        events.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));

        return Task.FromResult((IReadOnlyList<EVCR003A>)events);
    }
}
