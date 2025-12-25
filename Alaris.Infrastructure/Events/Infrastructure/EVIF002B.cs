// =============================================================================
// EVIF002B.cs - File-Based Persistent Audit Logger
// Component: EVIF002B | Category: Infrastructure | Variant: B (Persistent)
// =============================================================================
// Append-only file storage for audit logs with queryable indices.
// Production replacement for EVIF002A (in-memory).
// =============================================================================
// Rule 17 (Audibility): Audit logs are never modified or deleted.
// =============================================================================

using System.Text.Json;
using Alaris.Infrastructure.Events.Core;

namespace Alaris.Infrastructure.Events.Infrastructure;

/// <summary>
/// File-based persistent implementation of EVCR004A for production use.
/// </summary>
/// <remarks>
/// <para>
/// Storage format: JSON Lines (.jsonl)
/// - One audit entry per line
/// - Append-only (immutable once written)
/// - Date-based file rotation for manageability
/// </para>
/// </remarks>
public sealed class EVIF002B : EVCR004A, IDisposable
{
    private readonly string _storagePath;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly SemaphoreSlim _readSemaphore = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new file-based audit logger.
    /// </summary>
    /// <param name="storagePath">Directory for audit log files.</param>
    public EVIF002B(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        Directory.CreateDirectory(storagePath);
        _storagePath = storagePath;
    }

    /// <inheritdoc/>
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entry);

        // Serialize entry
        string json = JsonSerializer.Serialize(entry, JsonOptions);

        // Get the appropriate log file (date-based)
        string logPath = GetLogPathForDate(entry.OccurredAtUtc);

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream fs = new(
                logPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            await using StreamWriter writer = new(fs);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetEntriesForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        List<AuditEntry> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (AuditEntry entry in ReadAllEntriesAsync(cancellationToken))
            {
                if (entry.EntityType == entityType && entry.EntityId == entityId)
                {
                    result.Add(entry);
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.OccurredAtUtc).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetEntriesByInitiatorAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatedBy);

        List<AuditEntry> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (AuditEntry entry in ReadAllEntriesAsync(cancellationToken))
            {
                if (entry.InitiatedBy == initiatedBy)
                {
                    result.Add(entry);
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.OccurredAtUtc).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetEntriesByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<AuditEntry> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Only scan relevant monthly files
            string[] relevantFiles = GetLogFilesInRange(fromUtc, toUtc);

            foreach (string logPath in relevantFiles)
            {
                await foreach (AuditEntry entry in ReadEntriesFromFileAsync(logPath, cancellationToken))
                {
                    if (entry.OccurredAtUtc >= fromUtc && entry.OccurredAtUtc <= toUtc)
                    {
                        result.Add(entry);
                    }
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.OccurredAtUtc).ToList();
    }

    private string GetLogPathForDate(DateTime date)
    {
        return Path.Combine(_storagePath, $"audit-{date:yyyy-MM}.jsonl");
    }

    private string[] GetLogFilesInRange(DateTime fromUtc, DateTime toUtc)
    {
        if (!Directory.Exists(_storagePath))
        {
            return [];
        }

        // Get all audit files and filter to relevant date range
        return Directory.GetFiles(_storagePath, "audit-*.jsonl")
            .Where(f =>
            {
                string fileName = Path.GetFileNameWithoutExtension(f);
                if (fileName.Length < 12)
                {
                    return false;
                }

                // Parse audit-yyyy-MM
                string dateStr = fileName.Replace("audit-", "");
                if (DateTime.TryParseExact(dateStr, "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime fileMonth))
                {
                    DateTime fileStart = new(fileMonth.Year, fileMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime fileEnd = fileStart.AddMonths(1).AddTicks(-1);
                    return fileStart <= toUtc && fileEnd >= fromUtc;
                }
                return false;
            })
            .OrderBy(f => f)
            .ToArray();
    }

    private async IAsyncEnumerable<AuditEntry> ReadAllEntriesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_storagePath))
        {
            yield break;
        }

        string[] logFiles = Directory.GetFiles(_storagePath, "audit-*.jsonl").OrderBy(f => f).ToArray();

        foreach (string logPath in logFiles)
        {
            await foreach (AuditEntry entry in ReadEntriesFromFileAsync(logPath, cancellationToken))
            {
                yield return entry;
            }
        }
    }

    private static async IAsyncEnumerable<AuditEntry> ReadEntriesFromFileAsync(
        string logPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(logPath))
        {
            yield break;
        }

        await using FileStream fs = new(
            logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using StreamReader reader = new(fs);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            AuditEntry? entry = null;
            try
            {
                entry = JsonSerializer.Deserialize<AuditEntry>(line, JsonOptions);
            }
            catch (JsonException)
            {
                // Skip corrupted lines
                continue;
            }

            if (entry != null)
            {
                yield return entry;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writeSemaphore.Dispose();
        _readSemaphore.Dispose();
        _disposed = true;
    }
}
