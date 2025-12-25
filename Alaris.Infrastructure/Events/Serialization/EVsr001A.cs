// EVsr001A.cs - Event binary serialization adapter (SBE format)

using System.Buffers.Binary;
using Alaris.Infrastructure.Events.Core;
using Alaris.Infrastructure.Protocol.Buffers;

namespace Alaris.Infrastructure.Events.Serialization;

/// <summary>
/// Binary serialization adapter for event envelopes.
/// Component ID: EVsr001A
/// </summary>
/// <remarks>
/// Provides dual-mode serialization:
/// - Binary (SBE-style) for hot path storage
/// - JSON for debugging and human-readable audit logs
/// 
/// Rule 17 Compliance: All serialized events remain immutable and traceable.
/// </remarks>
public static class EVsr001A
{
    private const byte FormatVersion = 1;

    /// <summary>
    /// Encodes an EventEnvelope to binary format.
    /// </summary>
    /// <param name="envelope">Source event envelope.</param>
    /// <param name="buffer">Target buffer (minimum 4KB recommended).</param>
    /// <returns>Number of bytes written.</returns>
    public static int EncodeEventEnvelope(EVCR003A envelope, Span<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        
        int offset = 0;

        // Version byte
        buffer[offset++] = FormatVersion;

        // EventId (16 bytes GUID)
        envelope.EventId.TryWriteBytes(buffer[offset..]);
        offset += 16;

        // SequenceNumber (8 bytes)
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], envelope.SequenceNumber);
        offset += 8;

        // StoredAtUtc as Unix epoch ms (8 bytes)
        BinaryPrimitives.WriteInt64LittleEndian(
            buffer[offset..],
            new DateTimeOffset(envelope.StoredAtUtc, TimeSpan.Zero).ToUnixTimeMilliseconds());
        offset += 8;

        // EventType (fixed 64 chars)
        WriteFixedString(buffer[offset..], envelope.EventType, 64);
        offset += 64;

        // AggregateId (fixed 64 chars, nullable)
        WriteFixedString(buffer[offset..], envelope.AggregateId, 64);
        offset += 64;

        // CorrelationId (fixed 36 chars, nullable)
        WriteFixedString(buffer[offset..], envelope.CorrelationId, 36);
        offset += 36;

        // CausationId (fixed 36 chars, nullable)
        WriteFixedString(buffer[offset..], envelope.CausationId, 36);
        offset += 36;

        // InitiatedBy (fixed 64 chars, nullable)
        WriteFixedString(buffer[offset..], envelope.InitiatedBy, 64);
        offset += 64;

        // EventData length + variable data
        byte[] eventDataBytes = System.Text.Encoding.UTF8.GetBytes(envelope.EventData);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], eventDataBytes.Length);
        offset += 4;
        eventDataBytes.CopyTo(buffer[offset..]);
        offset += eventDataBytes.Length;

        return offset;
    }

    /// <summary>
    /// Decodes an EventEnvelope from binary format.
    /// </summary>
    public static EVCR003A DecodeEventEnvelope(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        // Version check
        byte version = buffer[offset++];
        if (version != FormatVersion)
        {
            throw new InvalidOperationException($"Unsupported event format version: {version}");
        }

        // EventId
        var eventId = new Guid(buffer.Slice(offset, 16));
        offset += 16;

        // SequenceNumber
        long sequenceNumber = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        // StoredAtUtc
        long storedAtMs = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        // EventType
        string eventType = ReadFixedString(buffer[offset..], 64);
        offset += 64;

        // AggregateId
        string? aggregateId = ReadNullableFixedString(buffer[offset..], 64);
        offset += 64;

        // CorrelationId
        string? correlationId = ReadNullableFixedString(buffer[offset..], 36);
        offset += 36;

        // CausationId
        string? causationId = ReadNullableFixedString(buffer[offset..], 36);
        offset += 36;

        // InitiatedBy
        string? initiatedBy = ReadNullableFixedString(buffer[offset..], 64);
        offset += 64;

        // EventData
        int eventDataLength = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        string eventData = System.Text.Encoding.UTF8.GetString(buffer.Slice(offset, eventDataLength));

        return new EVCR003A
        {
            EventId = eventId,
            SequenceNumber = sequenceNumber,
            StoredAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(storedAtMs).UtcDateTime,
            EventType = eventType,
            EventData = eventData,
            AggregateId = aggregateId,
            CorrelationId = correlationId,
            CausationId = causationId,
            InitiatedBy = initiatedBy
        };
    }

    /// <summary>
    /// Gets the estimated encoded size for an event envelope.
    /// </summary>
    public static int GetEncodedSize(EVCR003A envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        
        // Fixed: 1 (version) + 16 (guid) + 8 (seq) + 8 (time) + 64 + 64 + 36 + 36 + 64 + 4 (data length)
        // Variable: eventData bytes
        return 301 + System.Text.Encoding.UTF8.GetByteCount(envelope.EventData);
    }


    private static void WriteFixedString(Span<byte> buffer, string? value, int length)
    {
        buffer[..length].Clear();
        if (!string.IsNullOrEmpty(value))
        {
            int bytesToWrite = Math.Min(value.Length, length);
            System.Text.Encoding.ASCII.GetBytes(value.AsSpan(0, bytesToWrite), buffer);
        }
    }

    private static string ReadFixedString(ReadOnlySpan<byte> buffer, int length)
    {
        int actualLength = buffer[..length].IndexOf((byte)0);
        if (actualLength < 0)
        {
            actualLength = length;
        }

        return System.Text.Encoding.ASCII.GetString(buffer[..actualLength]);
    }

    private static string? ReadNullableFixedString(ReadOnlySpan<byte> buffer, int length)
    {
        string value = ReadFixedString(buffer, length);
        return string.IsNullOrEmpty(value) ? null : value;
    }

}
