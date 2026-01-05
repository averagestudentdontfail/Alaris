// PLSR001A.cs - Zero-allocation binary serialization for market data

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Alaris.Infrastructure.Protocol.Serialization;

/// <summary>
/// Zero-allocation binary serialization for Alaris market data types.
/// Component ID: PLSR001A
/// </summary>
/// <remarks>
/// This class implements SBE-style encoding with fixed layouts and direct memory access.
/// All encode/decode operations work on Span&lt;byte&gt; with no heap allocations.
/// 
/// Binary Format (little-endian):
/// - Header: 8 bytes (blockLength:2, templateId:2, schemaId:2, version:2)
/// - Fixed fields: at known offsets
/// - Variable-length data: at end with length prefix
/// </remarks>
public static class PLSR001A
{
    // Schema constants
    private const ushort SchemaId = 1;
    private const ushort SchemaVersion = 1;

    // Message template IDs
    private const ushort OptionContractTemplateId = 1;
    private const ushort PriceBarTemplateId = 2;
    private const ushort EarningsEventTemplateId = 3;
    private const ushort OptionChainSnapshotTemplateId = 4;
    private const ushort MarketDataSnapshotTemplateId = 5;

    // Header size
    private const int HeaderSize = 8;


    /// <summary>
    /// Encodes a PriceBar to binary format.
    /// </summary>
    /// <param name="bar">Source price bar.</param>
    /// <param name="buffer">Target buffer (minimum 64 bytes).</param>
    /// <returns>Number of bytes written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EncodePriceBar(in PriceBarData bar, Span<byte> buffer)
    {
        const int blockLength = 56; // Fixed field block size
        int offset = 0;

        // Write header
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], blockLength);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], PriceBarTemplateId);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], SchemaId);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], SchemaVersion);
        offset += 2;

        // Write fixed fields
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], bar.TimestampEpochMs);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], bar.OpenMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], bar.HighMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], bar.LowMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], bar.CloseMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], bar.Volume);
        offset += 8;

        return offset;
    }

    /// <summary>
    /// Decodes a PriceBar from binary format (zero allocation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PriceBarData DecodePriceBar(ReadOnlySpan<byte> buffer)
    {
        int offset = HeaderSize; // Skip header

        return new PriceBarData
        {
            TimestampEpochMs = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]),
            OpenMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 8)..]),
            HighMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 16)..]),
            LowMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 24)..]),
            CloseMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 32)..]),
            Volume = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 40)..])
        };
    }



    /// <summary>
    /// Encodes an OptionContract to binary format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EncodeOptionContract(in OptionContractData contract, Span<byte> buffer)
    {
        const int blockLength = 128; // Fixed field block size
        int offset = 0;

        // Write header
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], blockLength);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], OptionContractTemplateId);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], SchemaId);
        offset += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], SchemaVersion);
        offset += 2;

        // Write fixed fields
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.StrikeMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], contract.ExpirationDays);
        offset += 4;
        buffer[offset] = (byte)contract.Right;
        offset += 1;
        // Padding for alignment
        buffer[offset] = 0;
        buffer[offset + 1] = 0;
        buffer[offset + 2] = 0;
        offset += 3;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.BidMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.AskMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.LastMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.ImpliedVolatilityMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.DeltaMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.GammaMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.ThetaMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.VegaMantissa);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.OpenInterest);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.Volume);
        offset += 8;

        // Write symbol (fixed 16 chars)
        WriteFixedString(buffer[offset..], contract.Symbol, 16);
        offset += 16;

        return offset;
    }

    /// <summary>
    /// Decodes an OptionContract from binary format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static OptionContractData DecodeOptionContract(ReadOnlySpan<byte> buffer)
    {
        int offset = HeaderSize;

        var contract = new OptionContractData
        {
            StrikeMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]),
            ExpirationDays = BinaryPrimitives.ReadInt32LittleEndian(buffer[(offset + 8)..]),
            Right = (OptionRightEnum)buffer[offset + 12]
        };
        offset += 16; // Including padding

        contract.BidMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        contract.AskMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 8)..]);
        contract.LastMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 16)..]);
        contract.ImpliedVolatilityMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 24)..]);
        contract.DeltaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 32)..]);
        contract.GammaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 40)..]);
        contract.ThetaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 48)..]);
        contract.VegaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 56)..]);
        contract.OpenInterest = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 64)..]);
        contract.Volume = BinaryPrimitives.ReadInt64LittleEndian(buffer[(offset + 72)..]);
        offset += 80;

        contract.Symbol = ReadFixedString(buffer[offset..], 16);

        return contract;
    }



    /// <summary>
    /// Converts decimal to fixed-point mantissa (8 decimal places).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToMantissa(decimal value) => (long)(value * 100_000_000m);

    /// <summary>
    /// Converts fixed-point mantissa to decimal (8 decimal places).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal FromMantissa(long mantissa) => mantissa / 100_000_000m;

    /// <summary>
    /// Writes a fixed-length string (null-padded).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFixedString(Span<byte> buffer, string? value, int length)
    {
        buffer[..length].Clear();
        if (!string.IsNullOrEmpty(value))
        {
            int bytesToWrite = Math.Min(value.Length, length);
            Encoding.ASCII.GetBytes(value.AsSpan(0, bytesToWrite), buffer);
        }
    }

    /// <summary>
    /// Reads a fixed-length string (null-terminated).
    /// </summary>
    private static string ReadFixedString(ReadOnlySpan<byte> buffer, int length)
    {
        int actualLength = buffer[..length].IndexOf((byte)0);
        if (actualLength < 0)
        {
            actualLength = length;
        }

        return Encoding.ASCII.GetString(buffer[..actualLength]);
    }

}


/// <summary>
/// Binary-optimized price bar data structure.
/// All decimal values stored as fixed-point mantissa (8 decimal places).
/// </summary>
public struct PriceBarData
{
    public long TimestampEpochMs;
    public long OpenMantissa;
    public long HighMantissa;
    public long LowMantissa;
    public long CloseMantissa;
    public long Volume;

    public readonly DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampEpochMs).UtcDateTime;
    public readonly decimal Open => PLSR001A.FromMantissa(OpenMantissa);
    public readonly decimal High => PLSR001A.FromMantissa(HighMantissa);
    public readonly decimal Low => PLSR001A.FromMantissa(LowMantissa);
    public readonly decimal Close => PLSR001A.FromMantissa(CloseMantissa);
}

/// <summary>
/// Option right enumeration for binary encoding.
/// </summary>
public enum OptionRightEnum : byte
{
    Call = 0,
    Put = 1
}

/// <summary>
/// Binary-optimized option contract data structure.
/// </summary>
public struct OptionContractData
{
    public long StrikeMantissa;
    public int ExpirationDays;
    public OptionRightEnum Right;
    public long BidMantissa;
    public long AskMantissa;
    public long LastMantissa;
    public long ImpliedVolatilityMantissa;
    public long DeltaMantissa;
    public long GammaMantissa;
    public long ThetaMantissa;
    public long VegaMantissa;
    public long OpenInterest;
    public long Volume;
    public string? Symbol;

    public readonly decimal Strike => PLSR001A.FromMantissa(StrikeMantissa);
    public readonly decimal Bid => PLSR001A.FromMantissa(BidMantissa);
    public readonly decimal Ask => PLSR001A.FromMantissa(AskMantissa);
    public readonly decimal Last => PLSR001A.FromMantissa(LastMantissa);
    public readonly decimal ImpliedVolatility => PLSR001A.FromMantissa(ImpliedVolatilityMantissa);
    public readonly decimal Delta => PLSR001A.FromMantissa(DeltaMantissa);
    public readonly decimal Gamma => PLSR001A.FromMantissa(GammaMantissa);
    public readonly decimal Theta => PLSR001A.FromMantissa(ThetaMantissa);
    public readonly decimal Vega => PLSR001A.FromMantissa(VegaMantissa);
    public readonly DateTime Expiration => DateTime.UnixEpoch.AddDays(ExpirationDays);
}

