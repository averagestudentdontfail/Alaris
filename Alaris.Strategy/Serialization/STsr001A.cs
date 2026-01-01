// STsr001A.cs - strategy binary serialization adapter

using System.Buffers.Binary;
using Alaris.Infrastructure.Protocol.Serialization;

namespace Alaris.Strategy.Serialization;

/// <summary>
/// Binary serialization adapter for strategy hot path structures.
/// Component ID: STsr001A
/// </summary>

public static class STsr001A
{
    private const byte FormatVersion = 1;

    /// <summary>
    /// Struct for binary-encoded pricing parameters (STDT003A equivalent).
    /// </summary>
    public readonly struct PricingParamsData
    {
        public readonly long UnderlyingPriceMantissa;
        public readonly long StrikeMantissa;
        public readonly int ExpiryDays;
        public readonly int ValuationDays;
        public readonly long ImpliedVolMantissa;
        public readonly long RiskFreeRateMantissa;
        public readonly long DividendYieldMantissa;
        public readonly byte OptionType; // 0 = Call, 1 = Put

        public PricingParamsData(
            long underlyingPrice,
            long strike,
            int expiryDays,
            int valuationDays,
            long impliedVol,
            long riskFreeRate,
            long dividendYield,
            byte optionType)
        {
            UnderlyingPriceMantissa = underlyingPrice;
            StrikeMantissa = strike;
            ExpiryDays = expiryDays;
            ValuationDays = valuationDays;
            ImpliedVolMantissa = impliedVol;
            RiskFreeRateMantissa = riskFreeRate;
            DividendYieldMantissa = dividendYield;
            OptionType = optionType;
        }

        // Decoded properties for convenience
        public double UnderlyingPrice => (double)PLSR001A.FromMantissa(UnderlyingPriceMantissa);
        public double Strike => (double)PLSR001A.FromMantissa(StrikeMantissa);
        public double ImpliedVolatility => (double)PLSR001A.FromMantissa(ImpliedVolMantissa);
        public double RiskFreeRate => (double)PLSR001A.FromMantissa(RiskFreeRateMantissa);
        public double DividendYield => (double)PLSR001A.FromMantissa(DividendYieldMantissa);
    }

    /// <summary>
    /// Fixed size of encoded pricing parameters.
    /// Layout: 7*8 (mantissas) + 2*4 (days) + 1 (type) + 3 (padding) = 68 bytes
    /// </summary>
    public const int PricingParamsEncodedSize = 68;

    /// <summary>
    /// Encodes pricing parameters to binary format.
    /// </summary>
    public static int EncodePricingParams(
        double underlyingPrice,
        double strike,
        int expiryDays,
        int valuationDays,
        double impliedVol,
        double riskFreeRate,
        double dividendYield,
        bool isCall,
        Span<byte> buffer)
    {
        int offset = 0;

        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa((decimal)underlyingPrice));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa((decimal)strike));
        offset += 8;
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], expiryDays);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], valuationDays);
        offset += 4;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa((decimal)impliedVol));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa((decimal)riskFreeRate));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa((decimal)dividendYield));
        offset += 8;
        buffer[offset++] = isCall ? (byte)0 : (byte)1;
        // Padding for alignment
        buffer[offset++] = 0;
        buffer[offset++] = 0;
        buffer[offset++] = 0;

        return offset;
    }

    /// <summary>
    /// Decodes pricing parameters from binary format.
    /// </summary>
    public static PricingParamsData DecodePricingParams(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        long underlyingMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long strikeMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        int expiryDays = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        int valuationDays = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        long ivMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long rfMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long dyMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        byte optionType = buffer[offset];

        return new PricingParamsData(
            underlyingMantissa,
            strikeMantissa,
            expiryDays,
            valuationDays,
            ivMantissa,
            rfMantissa,
            dyMantissa,
            optionType);
    }

    /// <summary>
    /// Struct for binary-encoded term structure point.
    /// </summary>
    public readonly struct TermStructurePointData
    {
        public readonly int DaysToExpiry;
        public readonly long ImpliedVolMantissa;
        public readonly long OpenInterestWeight;

        public TermStructurePointData(int daysToExpiry, long impliedVolMantissa, long openInterestWeight)
        {
            DaysToExpiry = daysToExpiry;
            ImpliedVolMantissa = impliedVolMantissa;
            OpenInterestWeight = openInterestWeight;
        }

        public double ImpliedVolatility => (double)PLSR001A.FromMantissa(ImpliedVolMantissa);
    }

    /// <summary>
    /// Fixed size of encoded term structure point.
    /// Layout: 4 (days) + 8 (IV) + 8 (OI weight) = 20 bytes
    /// </summary>
    public const int TermPointEncodedSize = 20;

    /// <summary>
    /// Encodes a term structure point to binary format.
    /// </summary>
    public static int EncodeTermPoint(int daysToExpiry, double impliedVol, long openInterestWeight, Span<byte> buffer)
    {
        int offset = 0;

        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], daysToExpiry);
        offset += 4;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], PLSR001A.ToMantissa((decimal)impliedVol));
        offset += 8;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], openInterestWeight);
        offset += 8;

        return offset;
    }

    /// <summary>
    /// Decodes a term structure point from binary format.
    /// </summary>
    public static TermStructurePointData DecodeTermPoint(ReadOnlySpan<byte> buffer)
    {
        int offset = 0;

        int days = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]);
        offset += 4;
        long ivMantissa = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);
        offset += 8;
        long oiWeight = BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..]);

        return new TermStructurePointData(days, ivMantissa, oiWeight);
    }

    /// <summary>
    /// Encodes a term structure curve (multiple points) to binary format.
    /// </summary>
    public static int EncodeTermCurve(ReadOnlySpan<(int DaysToExpiry, double IV, long OIWeight)> points, Span<byte> buffer)
    {
        int offset = 0;

        // Version
        buffer[offset++] = FormatVersion;

        // Count
        BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], points.Length);
        offset += 4;

        // Points
        foreach (var (days, iv, oi) in points)
        {
            int written = EncodeTermPoint(days, iv, oi, buffer[offset..]);
            offset += written;
        }

        return offset;
    }

}
