// src/quantlib/ipc/message_types.h
#pragma once

#include "../core/time_type.h"
#include <cstdint>
#include <cstring>
#include <atomic>

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

// Cache-aligned market data message for TTA deterministic processing
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

    // Make it trivially copyable by removing custom constructors
    MarketDataMessage() = default;
};
static_assert(sizeof(MarketDataMessage) == 64, "MarketDataMessage must be exactly 64 bytes for TTA cache alignment");
static_assert(std::is_trivially_copyable_v<MarketDataMessage>, "MarketDataMessage must be trivially copyable");

// Cache-aligned trading signal message for TTA deterministic execution
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

    // Make it trivially copyable by removing custom constructors
    TradingSignalMessage() = default;
};
static_assert(sizeof(TradingSignalMessage) == 64, "TradingSignalMessage must be exactly 64 bytes for TTA cache alignment");
static_assert(std::is_trivially_copyable_v<TradingSignalMessage>, "TradingSignalMessage must be trivially copyable");

// Cache-aligned control message for TTA system coordination
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

    // Make it trivially copyable by removing custom constructors
    ControlMessage() = default;
};
static_assert(sizeof(ControlMessage) == 64, "ControlMessage must be exactly 64 bytes for TTA cache alignment");
static_assert(std::is_trivially_copyable_v<ControlMessage>, "ControlMessage must be trivially copyable");

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

// TTA message validation helper
template<typename MessageType>
inline bool validate_tta_message(const MessageType& msg) noexcept {
    static_assert(sizeof(MessageType) == 64, "TTA messages must be 64 bytes");
    return msg.is_valid();
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