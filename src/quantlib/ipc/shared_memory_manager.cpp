// src/quantlib/ipc/shared_memory_manager.cpp
#include "shared_memory.h" // Defines SharedMemoryManager and Message Types
#include "shared_ring_buffer.h" // For template SharedRingBuffer (though often .cpp includes .h)
                                // shared_memory.h should ideally include shared_ring_buffer.h if it uses it in its definition
#include <cstdio> // For fprintf, typically for error reporting to stderr
#include <stdexcept> // For std::runtime_error

// Ensure SharedRingBuffer template implementations are available.
// If SharedRingBuffer is fully defined in its header, this is fine.
// If it's a template class with implementation in a .cpp, explicit instantiation might be needed
// or the implementation moved to the header. The original shared_memory.cpp (IPC one)
// contained explicit instantiations.

namespace Alaris::IPC {

SharedMemoryManager::SharedMemoryManager(bool is_producer_role)
    : is_producer_(is_producer_role) {
    
    bool success = true;
    std::string error_message;

    try {
        market_data_buffer_ = std::make_unique<SharedRingBuffer<MarketDataMessage, 4096>>(
            "/alaris_market_data", is_producer_);
    } catch (const std::exception& e) {
        error_message += "Failed to initialize market_data_buffer: " + std::string(e.what()) + "\n";
        success = false;
    }
    
    try {
        signal_buffer_ = std::make_unique<SharedRingBuffer<TradingSignalMessage, 1024>>(
            "/alaris_signals", is_producer_);
    } catch (const std::exception& e) {
        error_message += "Failed to initialize signal_buffer: " + std::string(e.what()) + "\n";
        success = false;
    }
    
    try {
        control_buffer_ = std::make_unique<SharedRingBuffer<ControlMessage, 256>>(
            "/alaris_control", is_producer_);
    } catch (const std::exception& e) {
        error_message += "Failed to initialize control_buffer: " + std::string(e.what()) + "\n";
        success = false;
    }
            
    if (!success) {
        // Using fprintf for critical startup errors that might occur before full logging is up.
        fprintf(stderr, "SharedMemoryManager initialization failed:\n%s", error_message.c_str());
        // Depending on severity, you might re-throw or handle this to prevent application start.
        // For now, let's re-throw to make it explicit that initialization failed.
        throw std::runtime_error("SharedMemoryManager initialization failed. See stderr for details.");
    }
}

// Note: The default destructor std::unique_ptr will handle an ~SharedRingBuffer() is fine.

bool SharedMemoryManager::publish_market_data(const MarketDataMessage& data) {
    if (!is_producer_ || !market_data_buffer_) return false; // Cannot publish if not producer or buffer invalid
    return market_data_buffer_->try_write(data);
}

bool SharedMemoryManager::consume_market_data(MarketDataMessage& data) {
    if (is_producer_ || !market_data_buffer_) return false; // Cannot consume if producer or buffer invalid
    return market_data_buffer_->try_read(data);
}

size_t SharedMemoryManager::consume_market_data_batch(MarketDataMessage* data_array, size_t max_count) {
    if (is_producer_ || !market_data_buffer_) return 0;
    return market_data_buffer_->try_read_batch(data_array, max_count);
}

bool SharedMemoryManager::publish_signal(const TradingSignalMessage& signal) {
    if (!is_producer_ || !signal_buffer_) return false;
    return signal_buffer_->try_write(signal);
}

bool SharedMemoryManager::consume_signal(TradingSignalMessage& signal) {
    if (is_producer_ || !signal_buffer_) return false;
    return signal_buffer_->try_read(signal);
}

size_t SharedMemoryManager::consume_signal_batch(TradingSignalMessage* signals_array, size_t max_count) {
    if (is_producer_ || !signal_buffer_) return 0;
    return signal_buffer_->try_read_batch(signals_array, max_count);
}

bool SharedMemoryManager::publish_control(const ControlMessage& control) {
    // Control messages can typically be published by both producer (QuantLib) and consumer (Lean)
    // For example, Lean might send a STOP_TRADING control message.
    // QuantLib might send HEARTBEAT.
    // The is_producer_ check might be too restrictive here, or you need separate control channels.
    // For now, let's assume only the designated "producer" of main data also "produces" most control messages.
    // Or, allow both to publish to control_buffer.
    if (!control_buffer_) return false;
    return control_buffer_->try_write(control);
}

bool SharedMemoryManager::consume_control(ControlMessage& control) {
    // Similarly, both might consume certain control messages.
    if (!control_buffer_) return false;
    return control_buffer_->try_read(control);
}

SharedMemoryManager::BufferStatus SharedMemoryManager::get_status() const {
    BufferStatus status{}; // Initialize to zero/default

    if (market_data_buffer_) {
        status.market_data_size = market_data_buffer_->size();
        status.market_data_utilization = market_data_buffer_->utilization();
        status.market_data_total_messages = market_data_buffer_->total_writes(); // Assuming total_writes tracks messages pushed
    }
    if (signal_buffer_) {
        status.signal_size = signal_buffer_->size();
        status.signal_utilization = signal_buffer_->utilization();
        status.signal_total_messages = signal_buffer_->total_writes();
    }
    if (control_buffer_) {
        status.control_size = control_buffer_->size();
        status.control_utilization = control_buffer_->utilization();
        status.control_total_messages = control_buffer_->total_writes();
    }
    return status;
}

bool SharedMemoryManager::is_healthy() const {
    // A simple health check: buffers are initialized and not critically full.
    if (!market_data_buffer_ || !signal_buffer_ || !control_buffer_) {
        return false;
    }
    const auto current_status = get_status(); // Avoid repeated calls if get_status is expensive
    
    // Define "critically full" as, e.g., >95% utilization
    const double critical_threshold = 0.95; 
    
    return current_status.market_data_utilization < critical_threshold &&
           current_status.signal_utilization < critical_threshold &&
           current_status.control_utilization < critical_threshold;
}

void SharedMemoryManager::clear_all_buffers() {
    // This function is tricky. If called by a producer, it might clear data consumers haven't read.
    // If called by a consumer, it might lose data.
    // Generally, buffers should be cleared by the entity responsible for their creation/initialization
    // or based on a specific control signal.
    // This simplified version just attempts to drain them.
    MarketDataMessage market_msg;
    TradingSignalMessage signal_msg;
    ControlMessage control_msg;
    
    if (market_data_buffer_) while (market_data_buffer_->try_read(market_msg)) {}
    if (signal_buffer_)    while (signal_buffer_->try_read(signal_msg)) {}
    if (control_buffer_)   while (control_buffer_->try_read(control_msg)) {}
    
    // Note: This clear is from the perspective of a consumer. A producer clearing
    // might involve resetting head/tail pointers if it has exclusive write access.
    // The current SharedRingBuffer does not expose index resets directly.
}

} // namespace Alaris::IPC