using Alaris.Infrastructure.Events.Core;
using System.Collections.Concurrent;

namespace Alaris.Infrastructure.Events.Infrastructure;

/// <summary>
/// In-memory implementation of EVCR004A for development and testing.
/// NOT suitable for production use - audit logs are lost on process restart.
/// </summary>
/// <remarks>
/// For production, implement a persistent store using:
/// - SQL database with append-only table
/// - Dedicated audit log service
/// - File-based append-only logs with rotation
/// </remarks>
public sealed class EVIF002A : EVCR004A
{
    private readonly ConcurrentBag<AuditEntry> _entries = new ConcurrentBag<AuditEntry>();

    public Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        List<AuditEntry> entries = new List<AuditEntry>();
        foreach (AuditEntry entry in _entries)
        {
            if (entry.EntityType == entityType && entry.EntityId == entityId)
            {
                entries.Add(entry);
            }
        }

        entries.Sort(static (left, right) => left.OccurredAtUtc.CompareTo(right.OccurredAtUtc));

        return Task.FromResult((IReadOnlyList<AuditEntry>)entries);
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesByInitiatorAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        List<AuditEntry> entries = new List<AuditEntry>();
        foreach (AuditEntry entry in _entries)
        {
            if (entry.InitiatedBy == initiatedBy)
            {
                entries.Add(entry);
            }
        }

        entries.Sort(static (left, right) => left.OccurredAtUtc.CompareTo(right.OccurredAtUtc));

        return Task.FromResult((IReadOnlyList<AuditEntry>)entries);
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        List<AuditEntry> entries = new List<AuditEntry>();
        foreach (AuditEntry entry in _entries)
        {
            if (entry.OccurredAtUtc >= fromUtc && entry.OccurredAtUtc <= toUtc)
            {
                entries.Add(entry);
            }
        }

        entries.Sort(static (left, right) => left.OccurredAtUtc.CompareTo(right.OccurredAtUtc));

        return Task.FromResult((IReadOnlyList<AuditEntry>)entries);
    }
}
