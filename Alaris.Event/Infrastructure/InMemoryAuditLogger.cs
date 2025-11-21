using Alaris.Event.Core;
using System.Collections.Concurrent;

namespace Alaris.Event.Infrastructure;

/// <summary>
/// In-memory implementation of IAuditLogger for development and testing.
/// NOT suitable for production use - audit logs are lost on process restart.
/// </summary>
/// <remarks>
/// For production, implement a persistent store using:
/// - SQL database with append-only table
/// - Dedicated audit log service
/// - File-based append-only logs with rotation
/// </remarks>
public sealed class InMemoryAuditLogger : IAuditLogger
{
    private readonly ConcurrentBag<AuditEntry> _entries = new();

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
        IReadOnlyList<AuditEntry> entries = _entries
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToList();

        return Task.FromResult(entries);
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesByInitiatorAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuditEntry> entries = _entries
            .Where(e => e.InitiatedBy == initiatedBy)
            .OrderBy(e => e.OccurredAtUtc)
            .ToList();

        return Task.FromResult(entries);
    }

    public Task<IReadOnlyList<AuditEntry>> GetEntriesByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuditEntry> entries = _entries
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc <= toUtc)
            .OrderBy(e => e.OccurredAtUtc)
            .ToList();

        return Task.FromResult(entries);
    }
}
