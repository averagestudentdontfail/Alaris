// src/quantlib/ipc/shared_memory.h
#pragma once

#include "shared_ring_buffer.h"
#include "message_types.h"
#include <memory>
#include <string>

namespace Alaris::IPC {

class SharedMemoryManager {
private:
    std::unique_ptr<SharedRingBuffer<MarketDataMessage, 4096>> market_data_buffer_;
    std::unique_ptr<SharedRingBuffer<TradingSignalMessage, 1024>> signal_buffer_;
    std::unique_ptr<SharedRingBuffer<ControlMessage, 256>> control_buffer_;
    
    bool is_producer_;
    
public:
    explicit SharedMemoryManager(bool is_producer = true);
    ~SharedMemoryManager() = default;
    
    // Non-copyable, movable
    SharedMemoryManager(const SharedMemoryManager&) = delete;
    SharedMemoryManager& operator=(const SharedMemoryManager&) = delete;
    SharedMemoryManager(SharedMemoryManager&&) = default;
    SharedMemoryManager& operator=(SharedMemoryManager&&) = default;
    
    // Market data operations
    bool publish_market_data(const MarketDataMessage& data);
    bool consume_market_data(MarketDataMessage& data);
    size_t consume_market_data_batch(MarketDataMessage* data, size_t max_count);
    
    // Trading signal operations
    bool publish_signal(const TradingSignalMessage& signal);
    bool consume_signal(TradingSignalMessage& signal);
    size_t consume_signal_batch(TradingSignalMessage* signals, size_t max_count);
    
    // Control operations
    bool publish_control(const ControlMessage& control);
    bool consume_control(ControlMessage& control);
    
    // Status and monitoring
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
    
    BufferStatus get_status() const;
    
    // Utility functions
    void clear_all_buffers();
    bool is_healthy() const;
};

} // namespace Alaris::IPC