// src/quantlib/ipc/process_manager.cpp
#include "shared_memory.h"
#include <sys/types.h>
#include <unistd.h>
#include <signal.h>
#include <cstdio>

namespace Alaris::IPC {

SharedMemoryManager::SharedMemoryManager(bool is_producer) 
    : is_producer_(is_producer) {
    
    try {
        market_data_buffer_ = std::make_unique<SharedRingBuffer<MarketDataMessage, 4096>>(
            "/alaris_market_data", is_producer);
        
        signal_buffer_ = std::make_unique<SharedRingBuffer<TradingSignalMessage, 1024>>(
            "/alaris_signals", is_producer);
        
        control_buffer_ = std::make_unique<SharedRingBuffer<ControlMessage, 256>>(
            "/alaris_control", is_producer);
            
    } catch (const std::exception& e) {
        fprintf(stderr, "Failed to initialize shared memory: %s\n", e.what());
        throw;
    }
}

bool SharedMemoryManager::publish_market_data(const MarketDataMessage& data) {
    if (!is_producer_) return false;
    return market_data_buffer_->try_write(data);
}

bool SharedMemoryManager::consume_market_data(MarketDataMessage& data) {
    return market_data_buffer_->try_read(data);
}

size_t SharedMemoryManager::consume_market_data_batch(MarketDataMessage* data, size_t max_count) {
    return market_data_buffer_->try_read_batch(data, max_count);
}

bool SharedMemoryManager::publish_signal(const TradingSignalMessage& signal) {
    if (!is_producer_) return false;
    return signal_buffer_->try_write(signal);
}

bool SharedMemoryManager::consume_signal(TradingSignalMessage& signal) {
    return signal_buffer_->try_read(signal);
}

size_t SharedMemoryManager::consume_signal_batch(TradingSignalMessage* signals, size_t max_count) {
    return signal_buffer_->try_read_batch(signals, max_count);
}

bool SharedMemoryManager::publish_control(const ControlMessage& control) {
    return control_buffer_->try_write(control);
}

bool SharedMemoryManager::consume_control(ControlMessage& control) {
    return control_buffer_->try_read(control);
}

SharedMemoryManager::BufferStatus SharedMemoryManager::get_status() const {
    BufferStatus status;
    
    status.market_data_size = market_data_buffer_->size();
    status.signal_size = signal_buffer_->size();
    status.control_size = control_buffer_->size();
    
    status.market_data_utilization = market_data_buffer_->utilization();
    status.signal_utilization = signal_buffer_->utilization();
    status.control_utilization = control_buffer_->utilization();
    
    status.market_data_total_messages = market_data_buffer_->total_writes();
    status.signal_total_messages = signal_buffer_->total_writes();
    status.control_total_messages = control_buffer_->total_writes();
    
    return status;
}

bool SharedMemoryManager::is_healthy() const {
    const auto status = get_status();
    
    // Check for buffer overflow conditions
    return status.market_data_utilization < 0.9 &&
           status.signal_utilization < 0.9 &&
           status.control_utilization < 0.9;
}

void SharedMemoryManager::clear_all_buffers() {
    MarketDataMessage market_msg;
    TradingSignalMessage signal_msg;
    ControlMessage control_msg;
    
    // Drain all buffers
    while (market_data_buffer_->try_read(market_msg)) {}
    while (signal_buffer_->try_read(signal_msg)) {}
    while (control_buffer_->try_read(control_msg)) {}
}

} // namespace Alaris::IPC