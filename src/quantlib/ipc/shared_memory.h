// src/quantlib/ipc/shared_memory.h
#pragma once

#include "shared_ring_buffer.h"
#include "message_types.h"
#include "../core/time_type.h"
#include <memory>
#include <string>
#include <atomic>
#include <array>

namespace Alaris::IPC {

/**
 * @brief TTA-compliant shared memory manager for deterministic inter-process communication
 * 
 * Optimized for Time-Triggered Architecture with:
 * - Bounded execution times for all operations
 * - Deterministic memory access patterns
 * - Performance monitoring integration
 * - Automatic TTA health checking
 * - Zero dynamic allocation during operation
 */
class SharedMemoryManager {
private:
    // TTA-optimized ring buffers with compile-time sizing
    std::unique_ptr<SharedRingBuffer<MarketDataMessage, 4096>> market_data_buffer_;
    std::unique_ptr<SharedRingBuffer<TradingSignalMessage, 1024>> signal_buffer_;
    std::unique_ptr<SharedRingBuffer<ControlMessage, 256>> control_buffer_;
    
    // Process role and identification
    bool is_producer_;
    uint32_t process_id_;
    
    // TTA performance tracking
    mutable std::atomic<uint64_t> total_operations_{0};
    mutable std::atomic<uint64_t> failed_operations_{0};
    mutable std::atomic<uint64_t> timeout_events_{0};
    mutable Core::Timing::TimePoint last_health_check_;
    
    // TTA configuration
    struct TTAConfig {
        Core::Timing::Duration operation_timeout{std::chrono::microseconds(100)};
        size_t max_batch_size{32};
        bool enable_performance_monitoring{true};
        bool enable_automatic_health_checks{true};
        Core::Timing::Duration health_check_interval{std::chrono::seconds(1)};
    } tta_config_;
    
    // TTA buffer health tracking
    mutable std::array<bool, 3> buffer_health_status_{{true, true, true}};
    mutable Core::Timing::TimePoint last_buffer_health_update_;
    
public:
    /**
     * @brief Construct TTA-compliant shared memory manager
     * @param is_producer True if this process produces data (QuantLib), false if consumer (Lean)
     * @param process_id Unique identifier for this process in TTA system
     */
    explicit SharedMemoryManager(bool is_producer = true, uint32_t process_id = 1);
    
    /**
     * @brief Destructor with TTA-safe cleanup
     */
    ~SharedMemoryManager() = default;
    
    // Non-copyable for deterministic ownership, movable for efficient resource transfer
    SharedMemoryManager(const SharedMemoryManager&) = delete;
    SharedMemoryManager& operator=(const SharedMemoryManager&) = delete;
    SharedMemoryManager(SharedMemoryManager&&) = default;
    SharedMemoryManager& operator=(SharedMemoryManager&&) = default;
    
    // TTA-optimized market data operations (bounded execution time)
    
    /**
     * @brief Publish market data with TTA timing guarantees
     * @param data Market data message
     * @return True if published successfully
     * 
     * Execution time: Bounded to < 200ns typical, < 1μs worst case
     */
    bool publish_market_data(const MarketDataMessage& data);
    
    /**
     * @brief Consume single market data message with TTA guarantees
     * @param data Reference to store consumed message
     * @return True if message consumed successfully
     * 
     * Execution time: Bounded to < 150ns typical, < 500ns worst case
     */
    bool consume_market_data(MarketDataMessage& data);
    
    /**
     * @brief TTA-optimized batch market data consumption
     * @param data Array to store consumed messages
     * @param max_count Maximum number of messages to consume
     * @return Number of messages actually consumed
     * 
     * More efficient than multiple single consume calls for TTA scheduling
     */
    size_t consume_market_data_batch(MarketDataMessage* data, size_t max_count);
    
    // TTA-optimized trading signal operations
    
    /**
     * @brief Publish trading signal with TTA timing validation
     * @param signal Trading signal message
     * @return True if published successfully
     */
    bool publish_signal(const TradingSignalMessage& signal);
    
    /**
     * @brief Consume trading signal with TTA deadline checking
     * @param signal Reference to store consumed signal
     * @return True if valid, non-expired signal consumed
     */
    bool consume_signal(TradingSignalMessage& signal);
    
    /**
     * @brief TTA-optimized batch signal consumption with deadline filtering
     * @param signals Array to store consumed signals
     * @param max_count Maximum number of signals to consume
     * @return Number of valid signals actually consumed
     */
    size_t consume_signal_batch(TradingSignalMessage* signals, size_t max_count);
    
    // TTA-optimized control operations
    
    /**
     * @brief Publish control message with TTA priority handling
     * @param control Control message
     * @return True if published successfully
     */
    bool publish_control(const ControlMessage& control);
    
    /**
     * @brief Consume control message with TTA priority ordering
     * @param control Reference to store consumed control message
     * @return True if control message consumed
     */
    bool consume_control(ControlMessage& control);
    
    // TTA status and monitoring interface
    
    /**
     * @brief Comprehensive TTA buffer status
     */
    struct TTABufferStatus {
        // Current buffer utilization
        size_t market_data_size;
        size_t signal_size;
        size_t control_size;
        
        // Utilization percentages
        double market_data_utilization;
        double signal_utilization;
        double control_utilization;
        
        // Performance counters
        uint64_t market_data_total_messages;
        uint64_t signal_total_messages;
        uint64_t control_total_messages;
        
        // TTA-specific metrics
        uint64_t total_operations;
        uint64_t failed_operations;
        uint64_t timeout_events;
        double operation_failure_rate;
        
        // TTA health indicators
        bool is_tta_healthy;
        bool market_data_healthy;
        bool signal_healthy;
        bool control_healthy;
        
        // TTA timing metrics
        Core::Timing::Duration max_operation_latency;
        Core::Timing::Duration avg_operation_latency;
        uint64_t deadline_misses;
    };
    
    /**
     * @brief Get comprehensive TTA buffer status
     * @return Detailed status for TTA monitoring and debugging
     */
    TTABufferStatus get_tta_status() const;
    
    /**
     * @brief Check if all buffers are healthy for TTA operation
     * @return True if operating within TTA parameters
     */
    bool is_tta_healthy() const;
    
    /**
     * @brief Perform TTA health check and update internal state
     * @return True if all systems healthy
     */
    bool perform_tta_health_check() const;
    
    /**
     * @brief Reset TTA performance counters
     */
    void reset_tta_metrics();
    
    /**
     * @brief Configure TTA operation parameters
     * @param config TTA configuration parameters
     */
    void configure_tta_parameters(const TTAConfig& config);
    
    // Legacy interface for backwards compatibility
    struct BufferStatus {
        size_t market_data_size;
        size_t signal_size;
        size_t control_size;
        double market_data_utilization;
        double signal_utilization;
        double control_utilization;
        uint64_t market_data_total_messages;
        uint64_t signal_total_messages;
        uint64_t control_total_messages;
    };
    
    /**
     * @brief Get basic buffer status (legacy interface)
     * @return Basic buffer status
     */
    BufferStatus get_status() const;
    
    /**
     * @brief Basic health check (legacy interface)
     * @return True if buffers not critically full
     */
    bool is_healthy() const;
    
    /**
     * @brief Clear all buffers (use with caution in TTA systems)
     */
    void clear_all_buffers();
    
private:
    /**
     * @brief Update TTA operation metrics
     */
    void update_tta_metrics(bool success) const;
    
    /**
     * @brief Validate message for TTA compliance
     */
    template<typename MessageType>
    bool validate_tta_message(const MessageType& msg) const;
    
    /**
     * @brief Check if operation should timeout (TTA deadline enforcement)
     */
    bool should_timeout(Core::Timing::TimePoint start_time) const;
    
    /**
     * @brief Update buffer health status for TTA monitoring
     */
    void update_buffer_health() const;
    
    /**
     * @brief TTA-safe error reporting (bounded execution time)
     */
    void report_tta_error(const std::string& operation, const std::string& error) const;
};

/**
 * @brief TTA-specific shared memory configuration
 */
struct TTASharedMemoryConfig {
    // Buffer sizes (must be powers of 2)
    size_t market_data_buffer_size = 4096;
    size_t signal_buffer_size = 1024;
    size_t control_buffer_size = 256;
    
    // TTA timing constraints
    Core::Timing::Duration max_operation_latency{std::chrono::microseconds(100)};
    Core::Timing::Duration health_check_interval{std::chrono::seconds(1)};
    
    // Performance monitoring
    bool enable_performance_monitoring = true;
    bool enable_deadline_enforcement = true;
    bool enable_automatic_health_checks = true;
    
    // Shared memory names
    std::string market_data_name = "/alaris_market_data";
    std::string signal_name = "/alaris_signals";
    std::string control_name = "/alaris_control";
    
    static TTASharedMemoryConfig default_config() {
        return TTASharedMemoryConfig{};
    }
    
    static TTASharedMemoryConfig high_performance_config() {
        TTASharedMemoryConfig config;
        config.market_data_buffer_size = 8192;
        config.signal_buffer_size = 2048;
        config.max_operation_latency = std::chrono::microseconds(50);
        config.enable_performance_monitoring = false;  // Minimal overhead
        return config;
    }
    
    static TTASharedMemoryConfig development_config() {
        TTASharedMemoryConfig config;
        config.market_data_buffer_size = 1024;  // Smaller for development
        config.signal_buffer_size = 512;
        config.control_buffer_size = 128;
        config.enable_performance_monitoring = true;   // Full monitoring
        config.enable_deadline_enforcement = false;     // Relaxed for debugging
        return config;
    }
};

} // namespace Alaris::IPC