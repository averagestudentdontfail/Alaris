// =============================================================================
// EVIF001B.cs - File-Based Persistent Event Store
// Component: EVIF001B | Category: Infrastructure | Variant: B (Persistent)
// =============================================================================
// Append-only file storage with crash recovery and sequence persistence.
// Production replacement for EVIF001A (in-memory).
// =============================================================================
// Rule 17 (Audibility): Events are never modified or deleted.
// =============================================================================

using System.Text.Json;
using Alaris.Infrastructure.Events.Core;

namespace Alaris.Infrastructure.Events.Infrastructure;

/// <summary>
/// File-based persistent implementation of EVCR002A for production use.
/// </summary>
/// <remarks>
/// <para>
/// Storage format: JSON Lines (.jsonl)
/// - One JSON object per line
/// - Append-only (immutable once written)
/// - Sequence tracked in separate .seq file
/// </para>
/// <para>
/// Crash recovery:
/// - On startup, scans event file to rebuild sequence
/// - Atomic sequence file writes via temp+rename
/// </para>
/// </remarks>
public sealed class EVIF001B : EVCR002A, IDisposable
{
    private readonly string _eventsPath;
    private readonly string _sequencePath;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly SemaphoreSlim _readSemaphore = new(1, 1);
    private long _currentSequence;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new file-based event store.
    /// </summary>
    /// <param name="storagePath">Directory for event storage files.</param>
    public EVIF001B(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        Directory.CreateDirectory(storagePath);
        _eventsPath = Path.Combine(storagePath, "events.jsonl");
        _sequencePath = Path.Combine(storagePath, "sequence.seq");

        // Recover sequence on startup
        _currentSequence = RecoverSequence();
    }

    /// <inheritdoc/>
    public async Task<EVCR003A> AppendAsync<TEvent>(
        TEvent domainEvent,
        string? aggregateId = null,
        string? aggregateType = null,
        string? initiatedBy = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) where TEvent : EVCR001A
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long sequenceNumber = ++_currentSequence;

            EVCR003A envelope = EVCR003A.Create(
                domainEvent,
                sequenceNumber,
                aggregateId,
                aggregateType,
                initiatedBy,
                metadata);

            // Serialize envelope
            string json = JsonSerializer.Serialize(envelope, JsonOptions);

            // Append to file
            await using FileStream fs = new(
                _eventsPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            await using StreamWriter writer = new(fs);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Persist sequence (atomic via temp+rename)
            PersistSequence(sequenceNumber);

            return envelope;
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EVCR003A>> GetEventsForAggregateAsync(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        List<EVCR003A> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (EVCR003A envelope in ReadEventsAsync(cancellationToken))
            {
                if (envelope.AggregateId == aggregateId)
                {
                    result.Add(envelope);
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.SequenceNumber).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EVCR003A>> GetEventsFromSequenceAsync(
        long fromSequenceNumber,
        int maxCount = 1000,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<EVCR003A> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (EVCR003A envelope in ReadEventsAsync(cancellationToken))
            {
                if (envelope.SequenceNumber >= fromSequenceNumber)
                {
                    result.Add(envelope);
                    if (result.Count >= maxCount)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.SequenceNumber).ToList();
    }

    /// <inheritdoc/>
    public Task<long> GetCurrentSequenceNumberAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.FromResult(_currentSequence);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EVCR003A>> GetEventsByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        List<EVCR003A> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (EVCR003A envelope in ReadEventsAsync(cancellationToken))
            {
                if (envelope.CorrelationId == correlationId)
                {
                    result.Add(envelope);
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.SequenceNumber).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EVCR003A>> GetEventsByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<EVCR003A> result = [];

        await _readSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await foreach (EVCR003A envelope in ReadEventsAsync(cancellationToken))
            {
                if (envelope.StoredAtUtc >= fromUtc && envelope.StoredAtUtc <= toUtc)
                {
                    result.Add(envelope);
                }
            }
        }
        finally
        {
            _readSemaphore.Release();
        }

        return result.OrderBy(e => e.SequenceNumber).ToList();
    }

    private async IAsyncEnumerable<EVCR003A> ReadEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_eventsPath))
        {
            yield break;
        }

        await using FileStream fs = new(
            _eventsPath,
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

            EVCR003A? envelope = null;
            try
            {
                envelope = JsonSerializer.Deserialize<EVCR003A>(line, JsonOptions);
            }
            catch (JsonException)
            {
                // Skip corrupted lines
                continue;
            }

            if (envelope != null)
            {
                yield return envelope;
            }
        }
    }

    private long RecoverSequence()
    {
        // Try to read sequence file first
        if (File.Exists(_sequencePath))
        {
            string content = File.ReadAllText(_sequencePath);
            if (long.TryParse(content.Trim(), out long seq))
            {
                return seq;
            }
        }

        // Fall back to scanning events file
        if (!File.Exists(_eventsPath))
        {
            return 0;
        }

        long maxSequence = 0;
        foreach (string line in File.ReadLines(_eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("sequenceNumber", out JsonElement seqElement))
                {
                    long seq = seqElement.GetInt64();
                    maxSequence = Math.Max(maxSequence, seq);
                }
            }
            catch (JsonException)
            {
                // Skip corrupted lines
            }
        }

        return maxSequence;
    }

    private void PersistSequence(long sequence)
    {
        // Atomic write: write to temp, then rename
        string tempPath = _sequencePath + ".tmp";
        File.WriteAllText(tempPath, sequence.ToString());
        File.Move(tempPath, _sequencePath, overwrite: true);
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
