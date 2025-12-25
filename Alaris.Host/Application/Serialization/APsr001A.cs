// =============================================================================
// APsr001A.cs - Session Binary Serialization Adapter
// Component: APsr001A | Category: Serialization | Variant: A (Primary)
// =============================================================================
// Provides binary serialization for session metadata using SBE format.
// Supplements existing JSON serialization for backward compatibility.
// =============================================================================
// References:
// - Alaris.Governance/Coding.md Rule 5 (Zero-Allocation Hot Paths)
// - Alaris.Protocol/Schemas/Session.xml
// =============================================================================

using System.Buffers.Binary;
using Alaris.Host.Application.Model;
using Alaris.Infrastructure.Protocol.Buffers;

namespace Alaris.Host.Application.Serialization;

/// <summary>
/// Binary serialization adapter for session metadata.
/// Component ID: APsr001A
/// </summary>
/// <remarks>
/// Provides binary encoding/decoding for session metadata and index files.
/// This is optional and sessions continue to use JSON for human-readability
/// in the main session.json file, with binary format available for performance.
/// </remarks>
public static class APsr001A
{
    private const byte FormatVersion = 1;

    /// <summary>
    /// Encodes session metadata to binary format.
    /// </summary>
    /// <param name="session">Source session metadata.</param>
    /// <param name="buffer">Target buffer (minimum 512 bytes).</param>
    /// <returns>Number of bytes written.</returns>
    public static int EncodeSessionMetadata(APmd001A session, Span<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(session);
        
        int offset = 0;

        // Version byte
        buffer[offset++] = FormatVersion;

        // SessionId (fixed 64 chars)
        WriteFixedString(buffer[offset..], session.SessionId, 64);
        offset += 64;

        // StartDate as epoch days
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], (int)(session.StartDate - DateTime.UnixEpoch).TotalDays);
        offset += 4;

        // EndDate as epoch days
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], (int)(session.EndDate - DateTime.UnixEpoch).TotalDays);
        offset += 4;

        // CreatedAt as epoch ms
        BinaryPrimitives.WriteInt64LittleEndian(
            buffer[offset..],
            new DateTimeOffset(session.CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds());
        offset += 8;

        // UpdatedAt as epoch ms
        BinaryPrimitives.WriteInt64LittleEndian(
            buffer[offset..],
            new DateTimeOffset(session.UpdatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds());
        offset += 8;

        // Status (1 byte enum)
        buffer[offset++] = (byte)session.Status;

        // SessionPath (fixed 256 chars)
        WriteFixedString(buffer[offset..], session.SessionPath, 256);
        offset += 256;

        // Symbol count + symbols
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], session.Symbols.Count);
        offset += 4;
        foreach (string symbol in session.Symbols)
        {
            WriteFixedString(buffer[offset..], symbol, 16);
            offset += 16;
        }

        return offset;
    }

    /// <summary>
    /// Gets the estimated encoded size for session metadata.
    /// </summary>
    public static int GetEncodedSize(APmd001A session)
    {
        ArgumentNullException.ThrowIfNull(session);
        // Fixed: 1 + 64 + 4 + 4 + 8 + 8 + 1 + 256 + 4 = 350 bytes
        // Variable: 16 bytes per symbol
        return 350 + (session.Symbols.Count * 16);
    }

    /// <summary>
    /// Decodes session metadata from binary format.
    /// </summary>
    public static APmd001A DecodeSessionMetadata(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        // Version check
        byte version = buffer[offset++];
        if (version != FormatVersion)
        {
            throw new InvalidOperationException($"Unsupported session format version: {version}");
        }

        // SessionId
        string sessionId = ReadFixedString(buffer[offset..], 64);
        offset += 64;

        // Dates
        int startDays = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int endDays = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;

        // Timestamps
        long createdMs = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long updatedMs = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        // Status
        SessionStatus status = (SessionStatus)buffer[offset++];

        // SessionPath
        string sessionPath = ReadFixedString(buffer[offset..], 256);
        offset += 256;

        // Symbols
        int symbolCount = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        List<string> symbols = new(symbolCount);
        for (int i = 0; i < symbolCount; i++)
        {
            symbols.Add(ReadFixedString(buffer[offset..], 16));
            offset += 16;
        }

        return new APmd001A
        {
            SessionId = sessionId,
            StartDate = DateTime.UnixEpoch.AddDays(startDays),
            EndDate = DateTime.UnixEpoch.AddDays(endDays),
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(createdMs).UtcDateTime,
            UpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(updatedMs).UtcDateTime,
            Status = status,
            SessionPath = sessionPath,
            Symbols = symbols
        };
    }

    #region Helper Methods

    private static void WriteFixedString(Span<byte> buffer, string? value, int length)
    {
        buffer[..length].Clear();
        if (!string.IsNullOrEmpty(value))
        {
            int bytesToWrite = Math.Min(value.Length, length);
            System.Text.Encoding.UTF8.GetBytes(value.AsSpan(0, bytesToWrite), buffer);
        }
    }

    private static string ReadFixedString(ReadOnlySpan<byte> buffer, int length)
    {
        int actualLength = buffer[..length].IndexOf((byte)0);
        if (actualLength < 0)
        {
            actualLength = length;
        }

        return System.Text.Encoding.UTF8.GetString(buffer[..actualLength]);
    }

    #endregion
}
