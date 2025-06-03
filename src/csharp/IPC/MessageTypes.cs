// src/csharp/IPC/MessageTypes.cs 
using System;
using System.Runtime.InteropServices;

namespace Alaris.IPC
{
    // These structures MUST match the C++ IPC message types exactly
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MarketDataMessage
    {
        public ulong timestamp_ns;           // Match C++ snake_case naming
        public uint symbol_id;
        public double bid;
        public double ask;
        public double underlying_price;
        public double bid_iv;
        public double ask_iv;
        public uint bid_size;
        public uint ask_size;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] padding;

        public MarketDataMessage(uint symbolId, double bidPrice, double askPrice, double underlying)
        {
            timestamp_ns = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000; // Convert to nanoseconds
            symbol_id = symbolId;
            bid = bidPrice;
            ask = askPrice;
            underlying_price = underlying;
            bid_iv = 0.0;
            ask_iv = 0.0;
            bid_size = 0;
            ask_size = 0;
            padding = new byte[8];
        }
        
        // Properties for C# compatibility while maintaining C++ field names
        public ulong Timestamp => timestamp_ns;
        public uint SymbolId => symbol_id;
        public double Bid => bid;
        public double Ask => ask;
        public double UnderlyingPrice => underlying_price;
        public double BidIv => bid_iv;
        public double AskIv => ask_iv;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TradingSignalMessage
    {
        public ulong timestamp_ns;
        public uint symbol_id;
        public double theoretical_price;
        public double market_price;
        public double implied_volatility;
        public double forecast_volatility;
        public double confidence;
        public int quantity;
        public byte side; // 0=buy, 1=sell
        public byte urgency; // 0-255
        public byte signal_type; // 0=entry, 1=exit, 2=adjustment
        public byte reserved;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] padding;
        
        // Properties for C# compatibility
        public ulong Timestamp => timestamp_ns;
        public uint SymbolId => symbol_id;
        public double TheoreticalPrice => theoretical_price;
        public double MarketPrice => market_price;
        public double ImpliedVolatility => implied_volatility;
        public double ForecastVolatility => forecast_volatility;
        public double Confidence => confidence;
        public int Quantity => quantity;
        public byte Side => side;
        public byte Urgency => urgency;
        public byte SignalType => signal_type;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ControlMessage
    {
        public ulong timestamp_ns;
        public uint message_type;
        public uint sequence_number;        // Added to match your C++ usage
        public uint source_process_id;      // Added to match your C++ usage  
        public uint target_process_id;      // Added to match your C++ usage
        public uint priority;               // Added to match your C++ usage
        public double value1;
        public double value2;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] data;

        public ControlMessage(uint messageType)
        {
            timestamp_ns = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000;
            message_type = messageType;
            sequence_number = 0;
            source_process_id = 0;
            target_process_id = 0; 
            priority = 0;
            value1 = 0.0;
            value2 = 0.0;
            data = new byte[24];
        }
        
        // Properties for C# compatibility
        public ulong Timestamp => timestamp_ns;
        public uint MessageType => message_type;
        public uint SequenceNumber => sequence_number;
        public uint SourceProcessId => source_process_id;
        public uint TargetProcessId => target_process_id;
        public uint Priority => priority;
    }

    // Enums to match your C++ definitions
    public enum ControlMessageType : uint
    {
        START_TRADING = 0,
        STOP_TRADING = 1,
        HEARTBEAT = 2,
        UPDATE_PARAMETERS = 3,
        RESET_MODELS = 4,
        SYSTEM_STATUS = 5,
        EMERGENCY_LIQUIDATION = 6
    }

    // Match your C++ TTAPriority enum
    public enum TTAPriority : uint
    {
        LOW = 10,
        MEDIUM = 50,
        HIGH = 100,
        CRITICAL = 255
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
}