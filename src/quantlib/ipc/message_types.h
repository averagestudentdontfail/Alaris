// src/quantlib/ipc/message_types.h
#pragma once

#include "../core/time_type.h"
#include <cstdint>
#include <cstring>
#include <atomic>
#include <chrono>

namespace Alaris::IPC {

/**
 * @brief TTA-optimized message structures for deterministic inter-process communication
 * 
 * All messages are designed for:
 * - Cache-line alignment (64-byte boundaries)
 * - Deterministic memory layout
 * - Minimal cache misses
 * - Zero dynamic allocation
 * - Bounded serialization/deserialization time
 */

// Free functions for message validation (outside the structs to maintain trivially copyable status)
namespace MessageValidation {
    inline bool is_valid_market_data(const MarketDataMessage& msg) noexcept {
        return msg.timestamp_ns > 0 && 
               msg.symbol_id > 0 && 
               msg.bid >= 0.0 && msg.ask >= 0.0 && 
               msg.bid <= msg.ask && 
               msg.underlying_price > 0.0;
    }

    inline bool is_valid_trading_signal(const TradingSignalMessage& msg) noexcept {
        return msg.timestamp_ns > 0 && 
               msg.symbol_id > 0 && 
               msg.confidence >= 0.0 && msg.confidence <= 1.0 &&
               (msg.side == 0 || msg.side == 1) &&
               msg.quantity != 0;
    }

    inline bool is_valid_control(const ControlMessage& msg) noexcept {
        return msg.message_type > 0 && msg.timestamp_ns > 0;
    }

    inline bool is_expired(const TradingSignalMessage& msg) noexcept {
        const auto now_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
        return msg.expiry_timestamp_ns > 0 && now_ns > msg.expiry_timestamp_ns;
    }

    inline void set_timestamp_now(MarketDataMessage& msg) noexcept {
        msg.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
    }

    inline void set_timestamp_now(TradingSignalMessage& msg) noexcept {
        msg.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
    }

    inline void set_timestamp_now(ControlMessage& msg) noexcept {
        msg.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
    }
}

// Cache-aligned market data message for TTA deterministic processing
#pragma pack(push, 1)  // Disable padding between members
struct alignas(64) MarketDataMessage {
    uint64_t timestamp_ns;      // 8 bytes
    uint32_t symbol_id;         // 4 bytes
    double bid;                 // 8 bytes
    double ask;                 // 8 bytes
    double underlying_price;    // 8 bytes
    double bid_iv;             // 8 bytes
    double ask_iv;             // 8 bytes
    uint32_t bid_size;         // 4 bytes
    uint32_t ask_size;         // 4 bytes
    uint32_t processing_sequence; // 4 bytes
    uint32_t source_process_id;   // 4 bytes
    uint8_t padding[4];        // 4 bytes padding to reach 64 bytes

    MarketDataMessage() = default;
};
#pragma pack(pop)

// Cache-aligned trading signal message for TTA deterministic execution
#pragma pack(push, 1)
struct alignas(64) TradingSignalMessage {
    uint64_t timestamp_ns;          // 8 bytes
    uint64_t expiry_timestamp_ns;   // 8 bytes
    uint32_t symbol_id;             // 4 bytes
    double theoretical_price;       // 8 bytes
    double market_price;            // 8 bytes
    double implied_volatility;      // 8 bytes
    double forecast_volatility;     // 8 bytes
    double confidence;              // 8 bytes
    double expected_profit;         // 8 bytes
    int32_t quantity;              // 4 bytes
    uint8_t side;                  // 1 byte
    uint8_t urgency;               // 1 byte
    uint8_t signal_type;           // 1 byte
    uint8_t model_source;          // 1 byte
    uint32_t sequence_number;       // 4 bytes
    uint32_t processing_deadline_us; // 4 bytes
    uint8_t padding[4];            // 4 bytes padding to reach 64 bytes

    TradingSignalMessage() = default;
};
#pragma pack(pop)

// Cache-aligned control message for TTA system coordination
#pragma pack(push, 1)
struct alignas(64) ControlMessage {
    uint64_t timestamp_ns;      // 8 bytes
    uint64_t sequence_number;   // 8 bytes
    uint32_t message_type;      // 4 bytes
    uint32_t source_process_id; // 4 bytes
    uint32_t target_process_id; // 4 bytes
    uint32_t priority;          // 4 bytes
    double value1;              // 8 bytes
    double value2;              // 8 bytes
    uint64_t parameter1;        // 8 bytes
    uint64_t parameter2;        // 8 bytes
    uint8_t data[8];           // 8 bytes (reduced from 32 to fit in 64 bytes)

    ControlMessage() = default;
};
#pragma pack(pop)

// Message types for TTA control channel operations
enum class ControlMessageType : uint32_t {
    UNKNOWN = 0,
    
    // System control (high priority)
    START_TRADING = 1,
    STOP_TRADING = 2,
    EMERGENCY_STOP = 3,
    SYSTEM_SHUTDOWN = 4,
    
    // Configuration control (medium priority) 
    UPDATE_PARAMETERS = 10,
    RESET_MODELS = 11,
    RELOAD_CONFIG = 12,
    SET_LOG_LEVEL = 13,
    
    // Monitoring and status (low priority)
    SYSTEM_STATUS = 20,
    HEARTBEAT = 21,
    PERFORMANCE_REQUEST = 22,
    HEALTH_CHECK = 23,
    
    // TTA-specific control
    TTA_SCHEDULE_UPDATE = 30,
    TTA_TIMING_SYNC = 31,
    TTA_PERFORMANCE_REPORT = 32,
    TTA_DEADLINE_WARNING = 33
};

// Update the validate_tta_message template to use the free functions
template<typename MessageType>
inline bool validate_tta_message(const MessageType& msg) noexcept {
    static_assert(sizeof(MessageType) == 64, "TTA messages must be 64 bytes");
    if constexpr (std::is_same_v<MessageType, MarketDataMessage>) {
        return MessageValidation::is_valid_market_data(msg);
    } else if constexpr (std::is_same_v<MessageType, TradingSignalMessage>) {
        return MessageValidation::is_valid_trading_signal(msg);
    } else if constexpr (std::is_same_v<MessageType, ControlMessage>) {
        return MessageValidation::is_valid_control(msg);
    }
    return false;
}

// TTA message timing helper
template<typename MessageType>
inline void set_tta_timestamp(MessageType& msg) noexcept {
    msg.set_timestamp_now();
}

// TTA message priority levels for system coordination
enum class TTAPriority : uint32_t {
    EMERGENCY = 0,      // Emergency stop, system critical
    CRITICAL = 1,       // Market data, trading signals
    HIGH = 2,           // Control messages, parameter updates
    NORMAL = 3,         // Status updates, heartbeats
    LOW = 4,            // Performance reports, logging
    BACKGROUND = 5      // Non-time-critical operations
};

} // namespace Alaris::IPC