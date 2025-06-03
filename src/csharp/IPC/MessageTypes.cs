// src/csharp/IPC/MessageTypes.cs 
using System;
using System.Runtime.InteropServices;

namespace Alaris.IPC
{
    // These structures MUST match the C++ IPC message types exactly
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MarketDataMessage
    {
        public ulong timestamp_ns;      // 8 bytes
        public uint symbol_id;          // 4 bytes
        public double bid;              // 8 bytes
        public double ask;              // 8 bytes
        public double underlying_price; // 8 bytes
        public double bid_iv;           // 8 bytes
        public double ask_iv;           // 8 bytes
        public uint bid_size;           // 4 bytes
        public uint ask_size;           // 4 bytes
        public uint processing_sequence;// 4 bytes
        public uint source_process_id;  // 4 bytes
        public fixed byte padding[4];   // 4 bytes

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
            processing_sequence = 0;
            source_process_id = 0;
            padding = new byte[4];
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
    public unsafe struct TradingSignalMessage
    {
        public ulong timestamp_ns;           // 8 bytes
        public ulong expiry_timestamp_ns;    // 8 bytes
        public uint symbol_id;               // 4 bytes
        public double theoretical_price;     // 8 bytes
        public double market_price;          // 8 bytes
        public double implied_volatility;    // 8 bytes
        public double forecast_volatility;   // 8 bytes
        public double confidence;            // 8 bytes
        public double expected_profit;       // 8 bytes
        public int quantity;                 // 4 bytes
        public byte side;                    // 1 byte
        public byte urgency;                 // 1 byte
        public byte signal_type;             // 1 byte
        public byte model_source;            // 1 byte
        public uint sequence_number;         // 4 bytes
        public uint processing_deadline_us;  // 4 bytes
        public fixed byte padding[4];        // 4 bytes

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
    public unsafe struct ControlMessage
    {
        public ulong timestamp_ns;       // 8 bytes
        public ulong sequence_number;    // 8 bytes
        public uint message_type;        // 4 bytes
        public uint source_process_id;   // 4 bytes
        public uint target_process_id;   // 4 bytes
        public uint priority;            // 4 bytes
        public double value1;            // 8 bytes
        public double value2;            // 8 bytes
        public ulong parameter1;         // 8 bytes
        public ulong parameter2;         // 8 bytes
        public fixed byte data[8];       // 8 bytes

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
            parameter1 = 0;
            parameter2 = 0;
            data = new byte[8];
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