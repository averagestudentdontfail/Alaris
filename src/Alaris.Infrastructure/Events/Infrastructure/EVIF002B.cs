// EVIF002B.cs - File-based persistent audit logger (binary protocol)

using System.Buffers.Binary;
using System.Text;
using Alaris.Infrastructure.Events.Core;

namespace Alaris.Infrastructure.Events.Infrastructure;

/// <summary>
/// File-based persistent implementation of EVCR004A for production use.
/// </summary>
/// <remarks>
/// <para>
/// Storage format: Binary (SBE-style)
/// - Fixed header: 48 bytes
/// - Variable fields: length-prefixed UTF-8 strings
/// - Date-based file rotation for manageability
/// </para>
/// <para>
/// Binary Layout per record:
/// [0-15]  AuditId (Guid - 16 bytes)
/// [16-23] OccurredAtUtc (long, ticks)
/// [24-27] ActionLength (int)
/// [28-31] EntityTypeLength (int)
/// [32-35] EntityIdLength (int)
/// [36-39] InitiatedByLength (int)
/// [40-43] DetailsLength (int)
/// [44-47] TotalRecordLength (int)
/// [48...] Variable-length string data
/// </para>
/// </remarks>
public sealed class EVIF002B : EVCR004A, IDisposable
{
    private readonly string _storagePath;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private readonly SemaphoreSlim _readSemaphore = new(1, 1);
    private bool _disposed;

    private const int HeaderSize = 48;
    private const int MaxRecordSize = 1024 * 1024; // 1MB max per record

    /// <summary>
    /// Initializes a new file-based audit logger with binary format.
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

        // Serialize entry to binary
        byte[] record = SerializeAuditEntry(entry);

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
            await fs.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
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

        List<AuditEntry> result = new List<AuditEntry>();

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

        result.Sort(static (left, right) => left.OccurredAtUtc.CompareTo(right.OccurredAtUtc));
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetEntriesByInitiatorAsync(
        string initiatedBy,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(initiatedBy);

        List<AuditEntry> result = new List<AuditEntry>();

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

        result.Sort(static (left, right) => left.OccurredAtUtc.CompareTo(right.OccurredAtUtc));
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntry>> GetEntriesByTimeRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<AuditEntry> result = new List<AuditEntry>();

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

        result.Sort(static (left, right) => left.OccurredAtUtc.CompareTo(right.OccurredAtUtc));
        return result;
    }


    private static byte[] SerializeAuditEntry(AuditEntry entry)
    {
        // Get UTF-8 bytes for variable-length fields
        byte[] actionBytes = Encoding.UTF8.GetBytes(entry.Action);
        byte[] entityTypeBytes = Encoding.UTF8.GetBytes(entry.EntityType);
        byte[] entityIdBytes = Encoding.UTF8.GetBytes(entry.EntityId);
        byte[] initiatedByBytes = Encoding.UTF8.GetBytes(entry.InitiatedBy);
        byte[] descriptionBytes = Encoding.UTF8.GetBytes(entry.Description);

        int variableLength = actionBytes.Length + entityTypeBytes.Length +
                            entityIdBytes.Length + initiatedByBytes.Length + descriptionBytes.Length;
        int totalLength = HeaderSize + variableLength;

        byte[] buffer = new byte[totalLength];
        Span<byte> span = buffer;

        // Write header
        int offset = 0;
        entry.AuditId.TryWriteBytes(span[offset..]);
        offset += 16;

        BinaryPrimitives.WriteInt64LittleEndian(span[offset..], entry.OccurredAtUtc.Ticks);
        offset += 8;

        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], actionBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], entityTypeBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], entityIdBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], initiatedByBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], descriptionBytes.Length);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], totalLength);
        offset += 4;

        // Write variable-length data
        actionBytes.CopyTo(span[offset..]);
        offset += actionBytes.Length;
        entityTypeBytes.CopyTo(span[offset..]);
        offset += entityTypeBytes.Length;
        entityIdBytes.CopyTo(span[offset..]);
        offset += entityIdBytes.Length;
        initiatedByBytes.CopyTo(span[offset..]);
        offset += initiatedByBytes.Length;
        descriptionBytes.CopyTo(span[offset..]);

        return buffer;
    }

    private static AuditEntry DeserializeAuditEntry(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        Guid auditId = new(buffer.Slice(offset, 16));
        offset += 16;

        long occurredTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        DateTime occurredAtUtc = new(occurredTicks, DateTimeKind.Utc);
        offset += 8;

        int actionLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int entityTypeLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int entityIdLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int initiatedByLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int descriptionLen = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        // Skip totalLength
        offset += 4;

        string action = Encoding.UTF8.GetString(buffer.Slice(offset, actionLen));
        offset += actionLen;
        string entityType = Encoding.UTF8.GetString(buffer.Slice(offset, entityTypeLen));
        offset += entityTypeLen;
        string entityId = Encoding.UTF8.GetString(buffer.Slice(offset, entityIdLen));
        offset += entityIdLen;
        string initiatedBy = Encoding.UTF8.GetString(buffer.Slice(offset, initiatedByLen));
        offset += initiatedByLen;
        string description = Encoding.UTF8.GetString(buffer.Slice(offset, descriptionLen));

        return new AuditEntry
        {
            AuditId = auditId,
            OccurredAtUtc = occurredAtUtc,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            InitiatedBy = initiatedBy,
            Description = description
        };
    }


    private string GetLogPathForDate(DateTime date)
    {
        return Path.Combine(_storagePath, $"audit-{date:yyyy-MM}.bin");
    }

    private string[] GetLogFilesInRange(DateTime fromUtc, DateTime toUtc)
    {
        if (!Directory.Exists(_storagePath))
        {
            return Array.Empty<string>();
        }

        // Get all audit files and filter to relevant date range
        string[] files = Directory.GetFiles(_storagePath, "audit-*.bin");
        List<string> filtered = new List<string>();

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Length < 12)
            {
                continue;
            }

            string dateStr = fileName.Replace("audit-", "");
            if (!DateTime.TryParseExact(
                    dateStr,
                    "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime fileMonth))
            {
                continue;
            }

            DateTime fileStart = new DateTime(fileMonth.Year, fileMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime fileEnd = fileStart.AddMonths(1).AddTicks(-1);
            if (fileStart <= toUtc && fileEnd >= fromUtc)
            {
                filtered.Add(file);
            }
        }

        filtered.Sort(StringComparer.Ordinal);
        return filtered.ToArray();
    }

    private async IAsyncEnumerable<AuditEntry> ReadAllEntriesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_storagePath))
        {
            yield break;
        }

        string[] logFiles = Directory.GetFiles(_storagePath, "audit-*.bin");
        Array.Sort(logFiles, StringComparer.Ordinal);

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

        byte[] headerBuffer = new byte[HeaderSize];
        byte[]? recordBuffer = null;

        while (fs.Position < fs.Length && !cancellationToken.IsCancellationRequested)
        {
            // Read header to get record length
            int headerRead = await fs.ReadAsync(headerBuffer, cancellationToken).ConfigureAwait(false);
            if (headerRead < HeaderSize)
            {
                break; // Incomplete header
            }

            int totalLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(44));
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

            AuditEntry entry;
            try
            {
                entry = DeserializeAuditEntry(recordBuffer.AsSpan(0, totalLength));
            }
            catch
            {
                // Skip corrupted records
                continue;
            }

            yield return entry;
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
