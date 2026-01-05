// DTsr001A.cs - Binary serialization bridge for Alaris data models

using System.Buffers.Binary;
using System.Collections.ObjectModel;
using Alaris.Infrastructure.Data.Model;
using Alaris.Infrastructure.Protocol.Buffers;
using Alaris.Infrastructure.Protocol.Serialization;

namespace Alaris.Infrastructure.Data.Serialization;

/// <summary>
/// Bridges Alaris.Data.Model types to binary serialization format.
/// Component ID: DTsr001A
/// </summary>
/// <remarks>
/// This adapter converts between the domain model types (OptionContract, PriceBar, etc.)
/// and the binary-optimized Protocol types for zero-allocation serialization.
/// 
/// Usage for cache operations:
/// <code>
/// using var buffer = PLBF001A.RentBuffer();
/// int length = DTsr001A.EncodePriceBar(bar, buffer.Span);
/// await stream.WriteAsync(buffer.Memory[..length]);
/// </code>
/// </remarks>
public static class DTsr001A
{
    // Binary format version for forward compatibility
    private const byte FormatVersion = 1;


    /// <summary>
    /// Encodes a PriceBar to binary format.
    /// </summary>
    /// <param name="bar">Source domain model.</param>
    /// <param name="buffer">Target buffer (minimum 80 bytes).</param>
    /// <returns>Number of bytes written.</returns>
    public static int EncodePriceBar(PriceBar bar, Span<byte> buffer)
    {
        PriceBarData data = new PriceBarData
        {
            TimestampEpochMs = new DateTimeOffset(bar.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            OpenMantissa = PLSR001A.ToMantissa(bar.Open),
            HighMantissa = PLSR001A.ToMantissa(bar.High),
            LowMantissa = PLSR001A.ToMantissa(bar.Low),
            CloseMantissa = PLSR001A.ToMantissa(bar.Close),
            Volume = bar.Volume
        };

        return PLSR001A.EncodePriceBar(in data, buffer);
    }

    /// <summary>
    /// Decodes a PriceBar from binary format.
    /// </summary>
    /// <param name="buffer">Source buffer with encoded data.</param>
    /// <param name="symbol">Symbol for the decoded bar.</param>
    /// <returns>Decoded domain model.</returns>
    public static PriceBar DecodePriceBar(ReadOnlySpan<byte> buffer, string symbol)
    {
        PriceBarData data = PLSR001A.DecodePriceBar(buffer);

        return new PriceBar
        {
            Symbol = symbol,
            Timestamp = data.Timestamp,
            Open = data.Open,
            High = data.High,
            Low = data.Low,
            Close = data.Close,
            Volume = data.Volume
        };
    }

    /// <summary>
    /// Encodes a list of PriceBars to binary format.
    /// </summary>
    /// <param name="bars">Source bars.</param>
    /// <param name="buffer">Target buffer (64 bytes per bar + header).</param>
    /// <returns>Number of bytes written.</returns>
    public static int EncodePriceBars(IReadOnlyList<PriceBar> bars, Span<byte> buffer)
    {
        int offset = 0;

        // Version byte
        buffer[offset++] = FormatVersion;

        // Write count header
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], bars.Count);
        offset += 4;

        // Write symbol once (first bar's symbol, 16 bytes fixed)
        string symbol = bars.Count > 0 ? bars[0].Symbol : string.Empty;
        WriteFixedString(buffer[offset..], symbol, 16);
        offset += 16;

        // Write each bar
        foreach (PriceBar bar in bars)
        {
            int written = EncodePriceBar(bar, buffer[offset..]);
            offset += written;
        }

        return offset;
    }

    /// <summary>
    /// Decodes a list of PriceBars from binary format.
    /// </summary>
    public static ReadOnlyCollection<PriceBar> DecodePriceBars(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        // Version check
        byte version = buffer[offset++];
        if (version != FormatVersion)
        {
            throw new InvalidOperationException($"Unsupported binary format version: {version}");
        }

        int count = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;

        string symbol = ReadFixedString(buffer[offset..], 16);
        offset += 16;

        List<PriceBar> result = new List<PriceBar>(count);

        for (int i = 0; i < count; i++)
        {
            result.Add(DecodePriceBar(buffer[offset..], symbol));
            offset += 64; // Fixed size per bar (header + fields)
        }

        return result.AsReadOnly();
    }



    /// <summary>
    /// Encodes an OptionContract to binary format.
    /// </summary>
    public static int EncodeOptionContract(OptionContract contract, Span<byte> buffer)
    {
        int offset = 0;

        // Fixed fields first (for direct memory access)
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Strike));
        offset += 8;
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], (int)(contract.Expiration - DateTime.UnixEpoch).TotalDays);
        offset += 4;
        buffer[offset++] = contract.Right == OptionRight.Call ? (byte)0 : (byte)1;
        // Padding
        buffer[offset++] = 0;
        buffer[offset++] = 0;
        buffer[offset++] = 0;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Bid));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Ask));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Last ?? 0m));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.ImpliedVolatility ?? 0m));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Delta ?? 0m));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Gamma ?? 0m));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Theta ?? 0m));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa(contract.Vega ?? 0m));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.OpenInterest);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], contract.Volume);
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], new DateTimeOffset(contract.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds());
        offset += 8;

        // Variable-length strings at end
        WriteFixedString(buffer[offset..], contract.UnderlyingSymbol, 16);
        offset += 16;
        WriteFixedString(buffer[offset..], contract.OptionSymbol, 32);
        offset += 32;

        return offset;
    }

    /// <summary>
    /// Gets the fixed size of an encoded OptionContract.
    /// Layout: Strike(8) + Expiration(4) + Right+Pad(4) + 10*Greeks/Prices(80) + Timestamp(8) + Underlying(16) + Option(32) = 152 bytes
    /// </summary>
    public const int OptionContractEncodedSize = 152;

    /// <summary>
    /// Decodes an OptionContract from binary format.
    /// </summary>
    public static OptionContract DecodeOptionContract(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        decimal strike = PLSR001A.FromMantissa(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]));
        offset += 8;
        int expirationDays = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        OptionRight right = buffer[offset] == 0 ? OptionRight.Call : OptionRight.Put;
        offset += 4; // Including padding
        decimal bid = PLSR001A.FromMantissa(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]));
        offset += 8;
        decimal ask = PLSR001A.FromMantissa(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]));
        offset += 8;
        long lastMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long ivMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long deltaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long gammaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long thetaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long vegaMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long openInterest = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long volume = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long timestampMs = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        string underlyingSymbol = ReadFixedString(buffer[offset..], 16);
        offset += 16;
        string optionSymbol = ReadFixedString(buffer[offset..], 32);

        return new OptionContract
        {
            UnderlyingSymbol = underlyingSymbol,
            OptionSymbol = optionSymbol,
            Strike = strike,
            Expiration = DateTime.UnixEpoch.AddDays(expirationDays),
            Right = right,
            Bid = bid,
            Ask = ask,
            Last = lastMantissa != 0 ? PLSR001A.FromMantissa(lastMantissa) : null,
            ImpliedVolatility = ivMantissa != 0 ? PLSR001A.FromMantissa(ivMantissa) : null,
            Delta = deltaMantissa != 0 ? PLSR001A.FromMantissa(deltaMantissa) : null,
            Gamma = gammaMantissa != 0 ? PLSR001A.FromMantissa(gammaMantissa) : null,
            Theta = thetaMantissa != 0 ? PLSR001A.FromMantissa(thetaMantissa) : null,
            Vega = vegaMantissa != 0 ? PLSR001A.FromMantissa(vegaMantissa) : null,
            OpenInterest = openInterest,
            Volume = volume,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime
        };
    }



    /// <summary>
    /// Encodes an OptionChainSnapshot to binary format.
    /// </summary>
    /// <param name="snapshot">Source snapshot.</param>
    /// <param name="buffer">Target buffer (use PLBF001A.LargeBufferSize for chains).</param>
    /// <returns>Number of bytes written.</returns>
    public static int EncodeOptionChainSnapshot(OptionChainSnapshot snapshot, Span<byte> buffer)
    {
        int offset = 0;

        // Header: version byte for format compatibility
        buffer[offset++] = FormatVersion;

        // Timestamp (8 bytes)
        BinaryPrimitives.WriteInt64LittleEndian(
            buffer[offset..],
            new DateTimeOffset(snapshot.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds());
        offset += 8;

        // Spot price (8 bytes)
        BinaryPrimitives.WriteInt64LittleEndian(
            buffer[offset..],
            PLSR001A.ToMantissa(snapshot.SpotPrice));
        offset += 8;

        // Symbol (16 bytes fixed)
        WriteFixedString(buffer[offset..], snapshot.Symbol, 16);
        offset += 16;

        // Contract count (4 bytes)
        BinaryPrimitives.WriteInt32LittleEndian(
            buffer[offset..],
            snapshot.Contracts.Count);
        offset += 4;

        // Contracts
        foreach (OptionContract contract in snapshot.Contracts)
        {
            int written = EncodeOptionContract(contract, buffer[offset..]);
            offset += written;
        }

        return offset;
    }

    /// <summary>
    /// Decodes an OptionChainSnapshot from binary format.
    /// </summary>
    public static OptionChainSnapshot DecodeOptionChainSnapshot(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        // Version check
        byte version = buffer[offset++];
        if (version != FormatVersion)
        {
            throw new InvalidOperationException($"Unsupported binary cache version: {version}");
        }

        // Timestamp
        long timestampMs = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        // Spot price
        long spotMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;

        // Symbol
        string symbol = ReadFixedString(buffer[offset..], 16);
        offset += 16;

        // Contract count
        int contractCount = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;

        // Contracts
        List<OptionContract> contracts = new List<OptionContract>(contractCount);
        for (int i = 0; i < contractCount; i++)
        {
            contracts.Add(DecodeOptionContract(buffer[offset..]));
            offset += OptionContractEncodedSize;
        }

        return new OptionChainSnapshot
        {
            Symbol = symbol,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime,
            SpotPrice = PLSR001A.FromMantissa(spotMantissa),
            Contracts = contracts
        };
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

}
