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
    // Timestamp in nanoseconds since epoch (TTA timing reference)
    uint64_t timestamp_ns;
    
    // Symbol identifier (mapped to integer for TTA efficiency) 
    uint32_t symbol_id;
    
    // Core pricing data (8-byte aligned for optimal access)
    double bid;
    double ask;
    double underlying_price;
    double bid_iv;
    double ask_iv;
    
    // Size information
    uint32_t bid_size;
    uint32_t ask_size;
    
    // TTA performance tracking
    uint32_t processing_sequence;  // For TTA ordering verification
    uint32_t source_process_id;    // Source identifier
    
    // Padding to ensure exactly 64-byte cache line alignment
    char padding[64 - (8 + 4 + 5*8 + 2*4 + 2*4)];
    
    // TTA-optimized constructor with minimal initialization overhead
    MarketDataMessage() noexcept : 
        timestamp_ns(0), symbol_id(0), bid(0.0), ask(0.0), 
        underlying_price(0.0), bid_iv(0.0), ask_iv(0.0),
        bid_size(0), ask_size(0), processing_sequence(0), source_process_id(0) {
        // Zero-initialize padding for deterministic memory layout
        std::memset(padding, 0, sizeof(padding));
    }
    
    // TTA-optimized copy constructor (bounded execution time)
    MarketDataMessage(const MarketDataMessage& other) noexcept {
        std::memcpy(this, &other, sizeof(MarketDataMessage));
    }
    
    // TTA-optimized assignment (bounded execution time)
    MarketDataMessage& operator=(const MarketDataMessage& other) noexcept {
        if (this != &other) {
            std::memcpy(this, &other, sizeof(MarketDataMessage));
        }
        return *this;
    }
    
    // TTA validation for data integrity
    bool is_valid() const noexcept {
        return timestamp_ns > 0 && 
               symbol_id > 0 && 
               bid >= 0.0 && ask >= 0.0 && 
               bid <= ask && 
               underlying_price > 0.0;
    }
    
    // TTA-optimized timestamp setting
    void set_timestamp_now() noexcept {
        timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
    }
};
static_assert(sizeof(MarketDataMessage) == 64, "MarketDataMessage must be exactly 64 bytes for TTA cache alignment");

// Cache-aligned trading signal message for TTA deterministic execution
struct alignas(64) TradingSignalMessage {
    // TTA timing information
    uint64_t timestamp_ns;
    uint64_t expiry_timestamp_ns;  // When signal expires for TTA scheduling
    
    // Trading signal data
    uint32_t symbol_id;
    double theoretical_price;
    double market_price;
    double implied_volatility;
    double forecast_volatility;
    double confidence;
    double expected_profit;        // For TTA decision making
    
    // Position and execution data
    int32_t quantity;
    uint8_t side;           // 0=buy, 1=sell
    uint8_t urgency;        // 0-255, higher = more urgent (TTA priority)
    uint8_t signal_type;    // 0=entry, 1=exit, 2=adjustment
    uint8_t model_source;   // Which volatility model generated this
    
    // TTA execution tracking
    uint32_t sequence_number;      // For TTA ordering
    uint32_t processing_deadline_us; // Max processing time in microseconds
    
    // Padding to maintain 64-byte alignment
    char padding[64 - (2*8 + 4 + 6*8 + 4 + 4*1 + 2*4)];
    
    // TTA-optimized constructor
    TradingSignalMessage() noexcept :
        timestamp_ns(0), expiry_timestamp_ns(0), symbol_id(0),
        theoretical_price(0.0), market_price(0.0), implied_volatility(0.0),
        forecast_volatility(0.0), confidence(0.0), expected_profit(0.0),
        quantity(0), side(0), urgency(0), signal_type(0), model_source(0),
        sequence_number(0), processing_deadline_us(1000) {  // Default 1ms deadline
        std::memset(padding, 0, sizeof(padding));
    }
    
    // TTA-optimized copy operations
    TradingSignalMessage(const TradingSignalMessage& other) noexcept {
        std::memcpy(this, &other, sizeof(TradingSignalMessage));
    }
    
    TradingSignalMessage& operator=(const TradingSignalMessage& other) noexcept {
        if (this != &other) {
            std::memcpy(this, &other, sizeof(TradingSignalMessage));
        }
        return *this;
    }
    
    // TTA validation
    bool is_valid() const noexcept {
        return timestamp_ns > 0 && 
               symbol_id > 0 && 
               confidence >= 0.0 && confidence <= 1.0 &&
               (side == 0 || side == 1) &&
               quantity != 0;
    }
    
    // TTA deadline checking
    bool is_expired() const noexcept {
        const auto now_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
        return expiry_timestamp_ns > 0 && now_ns > expiry_timestamp_ns;
    }
    
    // TTA urgency calculation based on multiple factors
    void calculate_tta_urgency() noexcept {
        // Combine confidence, expected profit, and time sensitivity
        const double time_factor = is_expired() ? 0.0 : 1.0;
        const double profit_factor = std::max(0.0, std::min(1.0, expected_profit / 0.1)); // Normalize to 10% profit
        const double combined_urgency = confidence * profit_factor * time_factor;
        urgency = static_cast<uint8_t>(combined_urgency * 255.0);
    }
};
static_assert(sizeof(TradingSignalMessage) == 64, "TradingSignalMessage must be exactly 64 bytes for TTA cache alignment");

// Cache-aligned control message for TTA system coordination
struct alignas(64) ControlMessage {
    // TTA timing and identification
    uint64_t timestamp_ns;
    uint64_t sequence_number;
    
    // Control message data
    uint32_t message_type;
    uint32_t source_process_id;
    uint32_t target_process_id;  // 0 = broadcast
    uint32_t priority;           // TTA priority level
    
    // Parameter data for control operations
    double value1;
    double value2;
    uint64_t parameter1;
    uint64_t parameter2;
    
    // Variable data payload (32 bytes for TTA determinism)
    char data[32];
    
    // No padding needed - naturally aligns to 64 bytes
    
    // TTA-optimized constructors
    ControlMessage() noexcept :
        timestamp_ns(0), sequence_number(0), message_type(0),
        source_process_id(0), target_process_id(0), priority(0),
        value1(0.0), value2(0.0), parameter1(0), parameter2(0) {
        std::memset(data, 0, sizeof(data));
    }
    
    explicit ControlMessage(uint32_t type) noexcept :
        timestamp_ns(0), sequence_number(0), message_type(type),
        source_process_id(0), target_process_id(0), priority(0),
        value1(0.0), value2(0.0), parameter1(0), parameter2(0) {
        std::memset(data, 0, sizeof(data));
    }
    
    // TTA-optimized copy operations
    ControlMessage(const ControlMessage& other) noexcept {
        std::memcpy(this, &other, sizeof(ControlMessage));
    }
    
    ControlMessage& operator=(const ControlMessage& other) noexcept {
        if (this != &other) {
            std::memcpy(this, &other, sizeof(ControlMessage));
        }
        return *this;
    }
    
    // TTA validation
    bool is_valid() const noexcept {
        return message_type > 0 && timestamp_ns > 0;
    }
    
    // TTA-optimized string data setting (bounded execution time)
    void set_data_string(const char* str) noexcept {
        if (str) {
            size_t len = 0;
            // Bounded string length calculation
            while (len < sizeof(data) - 1 && str[len] != '\0') {
                ++len;
            }
            std::memcpy(data, str, len);
            data[len] = '\0';
            // Zero remaining bytes for deterministic memory layout
            if (len < sizeof(data) - 1) {
                std::memset(data + len + 1, 0, sizeof(data) - len - 1);
            }
        }
    }
    
    // TTA timestamp utilities
    void set_timestamp_now() noexcept {
        timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            Core::Timing::Clock::now().time_since_epoch()).count();
    }
};
static_assert(sizeof(ControlMessage) == 64, "ControlMessage must be exactly 64 bytes for TTA cache alignment");

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