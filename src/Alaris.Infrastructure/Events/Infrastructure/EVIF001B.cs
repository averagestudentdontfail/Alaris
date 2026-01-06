// EVIF001B.cs - File-based persistent event store (binary protocol)

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Alaris.Infrastructure.Events.Core;

namespace Alaris.Infrastructure.Events.Infrastructure;

/// <summary>
/// File-based persistent implementation of EVCR002A for production use.
/// </summary>
/// <remarks>
/// <para>
/// Storage format: Binary (SBE-style)
/// - Fixed header: 64 bytes (sequence, eventId, timestamps, lengths)
/// - Variable fields: length-prefixed UTF-8 strings
/// - Append-only (immutable once written)
/// </para>
/// <para>
/// Binary Layout per record:
/// [0-7]   SequenceNumber (long)
/// [8-23]  EventId (Guid - 16 bytes)
/// [24-31] StoredAtUtc (long, ticks)
/// [32-35] EventTypeLength (int)
/// [36-39] EventDataLength (int)
/// [40-43] AggregateIdLength (int)
/// [44-47] AggregateTypeLength (int)
/// [48-51] CorrelationIdLength (int)
/// [52-55] InitiatedByLength (int)
/// [56-59] TotalRecordLength (int)
/// [60-63] Reserved/Checksum
/// [64...] Variable-length string data
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

    private const int HeaderSize = 64;
    private const int MaxRecordSize = 1024 * 1024; // 1MB max per record

    /// <summary>
    /// Initializes a new file-based event store with binary format.
    /// </summary>
    /// <param name="storagePath">Directory for event storage files.</param>
    public EVIF001B(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        Directory.CreateDirectory(storagePath);
        _eventsPath = Path.Combine(storagePath, "events.bin");
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

            // Serialize to binary
            byte[] record = SerializeEnvelope(envelope);

            // Append to file
            await using FileStream fs = new(
                _eventsPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            await fs.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);

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

        List<EVCR003A> result = new List<EVCR003A>();

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

        result.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EVCR003A>> GetEventsFromSequenceAsync(
        long fromSequenceNumber,
        int maxCount = 1000,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<EVCR003A> result = new List<EVCR003A>();

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

        result.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
        return result;
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

        List<EVCR003A> result = new List<EVCR003A>();

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

        result.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EVCR003A>> GetEventsByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<EVCR003A> result = new List<EVCR003A>();

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

        result.Sort(static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
        return result;
    }


    private static byte[] SerializeEnvelope(EVCR003A envelope)
    {
        // Get UTF-8 bytes for variable-length fields
        byte[] eventTypeBytes = Encoding.UTF8.GetBytes(envelope.EventType);
        byte[] eventDataBytes = Encoding.UTF8.GetBytes(envelope.EventData);
        byte[] aggregateIdBytes = Encoding.UTF8.GetBytes(envelope.AggregateId ?? "");
        byte[] aggregateTypeBytes = Encoding.UTF8.GetBytes(envelope.AggregateType ?? "");
        byte[] correlationIdBytes = Encoding.UTF8.GetBytes(envelope.CorrelationId ?? "");
        byte[] initiatedByBytes = Encoding.UTF8.GetBytes(envelope.InitiatedBy ?? "");

        int variableLength = eventTypeBytes.Length + eventDataBytes.Length +
                            aggregateIdBytes.Length + aggregateTypeBytes.Length +
                            correlationIdBytes.Length + initiatedByBytes.Length;
        int totalLength = HeaderSize + variableLength;

        byte[] buffer = new byte[totalLength];
        Span<byte> span = buffer;

        // Write header
        int offset = 0;
        BinaryPrimitives.WriteInt64LittleEndian(span[offset..], envelope.SequenceNumber);
        offset += 8;

        envelope.EventId.TryWriteBytes(span[offset..]);
        offset += 16;

        BinaryPrimitives.WriteInt64LittleEndian(span[offset..], envelope.StoredAtUtc.Ticks);
        offset += 8;

        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], eventTypeBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], eventDataBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], aggregateIdBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], aggregateTypeBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], correlationIdBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], initiatedByBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], totalLength);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], 0); // Reserved
        offset += 4;

        // Write variable-length data
        eventTypeBytes.CopyTo(span[offset..]);
        offset += eventTypeBytes.Length;
        eventDataBytes.CopyTo(span[offset..]);
        offset += eventDataBytes.Length;
        aggregateIdBytes.CopyTo(span[offset..]);
        offset += aggregateIdBytes.Length;
        aggregateTypeBytes.CopyTo(span[offset..]);
        offset += aggregateTypeBytes.Length;
        correlationIdBytes.CopyTo(span[offset..]);
        offset += correlationIdBytes.Length;
        initiatedByBytes.CopyTo(span[offset..]);

        return buffer;
    }

    private static EVCR003A DeserializeEnvelope(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        long sequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        Guid eventId = new(buffer.Slice(offset, 16));
        offset += 16;

        long storedTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        DateTime storedAtUtc = new(storedTicks, DateTimeKind.Utc);
        offset += 8;

        int eventTypeLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int eventDataLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int aggregateIdLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int aggregateTypeLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int correlationIdLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int initiatedByLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        // Skip totalLength and reserved
        offset += 8;

        string eventType = Encoding.UTF8.GetString(buffer.Slice(offset, eventTypeLen));
        offset += eventTypeLen;
        string eventData = Encoding.UTF8.GetString(buffer.Slice(offset, eventDataLen));
        offset += eventDataLen;
        string? aggregateId = aggregateIdLen > 0 ?
            Encoding.UTF8.GetString(buffer.Slice(offset, aggregateIdLen)) : null;
        offset += aggregateIdLen;
        string? aggregateType = aggregateTypeLen > 0 ?
            Encoding.UTF8.GetString(buffer.Slice(offset, aggregateTypeLen)) : null;
        offset += aggregateTypeLen;
        string? correlationId = correlationIdLen > 0 ?
            Encoding.UTF8.GetString(buffer.Slice(offset, correlationIdLen)) : null;
        offset += correlationIdLen;
        string? initiatedBy = initiatedByLen > 0 ?
            Encoding.UTF8.GetString(buffer.Slice(offset, initiatedByLen)) : null;

        return new EVCR003A
        {
            EventId = eventId,
            SequenceNumber = sequenceNumber,
            StoredAtUtc = storedAtUtc,
            EventType = eventType,
            EventData = eventData,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            CorrelationId = correlationId,
            InitiatedBy = initiatedBy
        };
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

        byte[] headerBuffer = new byte[HeaderSize];
        byte[]? recordBuffer = null;

        while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
        {
            // Read header to get record length
            int headerRead = await fs.ReadAsync(headerBuffer, cancellationToken).ConfigureAwait(false);
            if (headerRead < HeaderSize)
            {
                break; // Incomplete header, file may be corrupted/truncated
            }

            int totalLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(56));
            if (totalLength <= 0 || totalLength > MaxRecordSize)
            {
                break; // Invalid record length
            }

            // Read full record
            if (recordBuffer == null || recordBuffer.Length < totalLength)
            {
                recordBuffer = new byte[totalLength];
            }

            headerBuffer.CopyTo(recordBuffer, 0);
            int remainingBytes = totalLength - HeaderSize;
            if (remainingBytes > 0)
            {
                int dataRead = await fs.ReadAsync(
                    recordBuffer.AsMemory(HeaderSize, remainingBytes),
                    cancellationToken).ConfigureAwait(false);
                if (dataRead < remainingBytes)
                {
                    break; // Incomplete record
                }
            }

            EVCR003A envelope;
            try
            {
                envelope = DeserializeEnvelope(recordBuffer.AsSpan(0, totalLength));
            }
            catch
            {
                // Skip corrupted records
                continue;
            }

            yield return envelope;
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

        // Fall back to scanning binary events file
        if (!File.Exists(_eventsPath))
        {
            return 0;
        }

        long maxSequence = 0;
        using FileStream fs = new(_eventsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] headerBuffer = new byte[HeaderSize];

        while (fs.Position < fs.Length)
        {
            int bytesRead = fs.Read(headerBuffer, 0, HeaderSize);
            if (bytesRead < HeaderSize)
            {
                break;
            }

            long seq = BinaryPrimitives.ReadInt64LittleEndian(headerBuffer);
            maxSequence = Math.Max(maxSequence, seq);

            int totalLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(56));
            if (totalLength <= HeaderSize || totalLength > MaxRecordSize)
            {
                break;
            }

            // Skip to next record
            int skipBytes = totalLength - HeaderSize;
            if (skipBytes > 0)
            {
                fs.Seek(skipBytes, SeekOrigin.Current);
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
