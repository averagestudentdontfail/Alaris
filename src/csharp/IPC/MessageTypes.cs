// src/csharp/IPC/MessageTypes.cs
using System;
using System.Runtime.InteropServices;

namespace Alaris.IPC
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MarketDataMessage
    {
        public ulong Timestamp;
        public uint SymbolId;
        public double Bid;
        public double Ask;
        public double UnderlyingPrice;
        public double BidIv;
        public double AskIv;
        public uint BidSize;
        public uint AskSize;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Padding;

        public MarketDataMessage(uint symbolId, double bid, double ask, double underlying)
        {
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000; // Convert to nanoseconds
            SymbolId = symbolId;
            Bid = bid;
            Ask = ask;
            UnderlyingPrice = underlying;
            BidIv = 0.0;
            AskIv = 0.0;
            BidSize = 0;
            AskSize = 0;
            Padding = new byte[8];
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TradingSignalMessage
    {
        public ulong Timestamp;
        public uint SymbolId;
        public double TheoreticalPrice;
        public double MarketPrice;
        public double ImpliedVolatility;
        public double ForecastVolatility;
        public double Confidence;
        public int Quantity;
        public byte Side; // 0=buy, 1=sell
        public byte Urgency; // 0-255
        public byte SignalType; // 0=entry, 1=exit, 2=adjustment
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public byte[] Padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ControlMessage
    {
        public ulong Timestamp;
        public uint MessageType;
        public uint Parameter1;
        public uint Parameter2;
        public double Value1;
        public double Value2;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Data;

        public ControlMessage(uint messageType)
        {
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;
            MessageType = messageType;
            Parameter1 = 0;
            Parameter2 = 0;
            Value1 = 0.0;
            Value2 = 0.0;
            Data = new byte[32];
        }
    }

    public enum StrategyMode
    {
        DeltaNeutral = 0,
        GammaScalping = 1,
        VolatilityTiming = 2,
        RelativeValue = 3
    }

    public enum MarketRegime
    {
        LowVol = 0,
        MediumVol = 1,
        HighVol = 2,
        Transitioning = 3
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MarketRegimeMessage
    {
        public ulong Timestamp;
        public MarketRegime VolRegime;
        public double CurrentRealizedVol;
        public double CurrentImpliedVol;
        public double VolRiskPremium;
        public double RegimeConfidence;
        public double ExpectedVolNextWeek;
        public double VolClusteringStrength;
        public double MeanReversionSpeed;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StrategyConfigMessage
    {
        public ulong Timestamp;
        public StrategyMode Mode;
        public double VolDifferenceThreshold;
        public double VolExitThreshold;
        public double ConfidenceThreshold;
        public double MaxPortfolioDelta;
        public double MaxPortfolioGamma;
        public double MaxPortfolioVega;
        public double MaxPositionSize;
        public double MaxCorrelationExposure;
        public double KellyFraction;
        public double MaxKellyPosition;
        public double MinEdgeRatio;
        public double StopLossPercent;
        public double ProfitTargetPercent;
        public double TrailingStopPercent;
        public bool AutoHedgeEnabled;
        public double HedgeThresholdDelta;
        public double HedgeThresholdGamma;
        public double HedgeFrequencyMinutes;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Padding;
    }

    public enum ControlMessageType
    {
        StartTrading = 0,
        StopTrading = 1,
        SystemStatus = 2,
        Heartbeat = 3,
        UpdateStrategyConfig = 4,
        UpdateMarketRegime = 5,
        EmergencyLiquidation = 6
    }
}