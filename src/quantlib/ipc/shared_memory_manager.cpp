#include "shared_memory.h"
#include "shared_ring_buffer.h"
#include "../core/time_type.h"
#include <cstdio>
#include <stdexcept>
#include <algorithm>

namespace Alaris::IPC {

SharedMemoryManager::SharedMemoryManager(bool is_producer_role, uint32_t process_id)
    : is_producer_(is_producer_role),
      process_id_(process_id),
      last_health_check_(Core::Timing::Clock::now()),
      last_buffer_health_update_(Core::Timing::Clock::now()) {
    
    std::string initialization_errors;
    bool all_buffers_initialized = true;

    // TTA-optimized buffer initialization with error aggregation using POSIX-compliant syntax
    try {
        market_data_buffer_ = std::make_unique<SharedRingBuffer<MarketDataMessage, 4096>>(
            "/alaris_market_data", is_producer_);
    } catch (const std::exception& e) {
        initialization_errors += "Market data buffer initialization failed: " + std::string(e.what()) + "\n";
        all_buffers_initialized = false;
    }
    
    try {
        signal_buffer_ = std::make_unique<SharedRingBuffer<TradingSignalMessage, 1024>>(
            "/alaris_signals", is_producer_);
    } catch (const std::exception& e) {
        initialization_errors += "Signal buffer initialization failed: " + std::string(e.what()) + "\n";
        all_buffers_initialized = false;
    }
    
    try {
        control_buffer_ = std::make_unique<SharedRingBuffer<ControlMessage, 256>>(
            "/alaris_control", is_producer_);
    } catch (const std::exception& e) {
        initialization_errors += "Control buffer initialization failed: " + std::string(e.what()) + "\n";
        all_buffers_initialized = false;
    }
    
    if (!all_buffers_initialized) {
        // TTA-safe error reporting to stderr for critical startup errors
        fprintf(stderr, "TTA SharedMemoryManager initialization failed (PID %u):\n%s", 
                process_id_, initialization_errors.c_str());
        throw std::runtime_error("TTA SharedMemoryManager: Critical buffer initialization failure");
    }
    
    // Initialize TTA performance tracking
    total_operations_.store(0, std::memory_order_relaxed);
    failed_operations_.store(0, std::memory_order_relaxed);
    timeout_events_.store(0, std::memory_order_relaxed);
    
    // Perform initial TTA health check
    perform_tta_health_check();
}

// Market data operations with TTA optimizations
bool SharedMemoryManager::publish_market_data(const MarketDataMessage& data) {
    if (!is_producer_ || !market_data_buffer_) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    // TTA timing tracking
    const auto start_time = Core::Timing::Clock::now();
    
    // TTA message validation (bounded execution time)
    if (!validate_tta_message(data)) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    // Check for TTA timeout before operation
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return false;
    }
    
    // Perform bounded-time write operation
    const bool success = market_data_buffer_->try_write(data);
    update_tta_metrics(success);
    
    return success;
}

bool SharedMemoryManager::consume_market_data(MarketDataMessage& data) {
    if (is_producer_ || !market_data_buffer_) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    
    // Check for TTA timeout before operation
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return false;
    }
    
    // Perform bounded-time read operation
    const bool success = market_data_buffer_->try_read(data);
    
    if (success) {
        // Additional TTA validation for consumed data
        if (!validate_tta_message(data)) [[unlikely]] {
            update_tta_metrics(false);
            return false;
        }
    }
    
    update_tta_metrics(success);
    return success;
}

size_t SharedMemoryManager::consume_market_data_batch(MarketDataMessage* data_array, size_t max_count) {
    if (is_producer_ || !market_data_buffer_ || !data_array) [[unlikely]] {
        update_tta_metrics(false);
        return 0;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    
    // TTA-compliant batch size limiting
    const size_t bounded_count = std::min(max_count, tta_config_.max_batch_size);
    
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return 0;
    }
    
    // Perform bounded-time batch read
    const size_t consumed = market_data_buffer_->try_read_batch(data_array, bounded_count);
    
    // TTA validation for batch (early termination on first invalid message)
    size_t valid_count = 0;
    for (size_t i = 0; i < consumed; ++i) {
        if (validate_tta_message(data_array[i])) {
            if (i != valid_count) {
                data_array[valid_count] = data_array[i];  // Compact valid messages
            }
            ++valid_count;
        } else {
            break;  // Stop processing on first invalid message for TTA determinism
        }
    }
    
    update_tta_metrics(valid_count > 0);
    return valid_count;
}

// Trading signal operations with TTA deadline checking
bool SharedMemoryManager::publish_signal(const TradingSignalMessage& signal) {
    if (!is_producer_ || !signal_buffer_) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    
    // TTA validation including deadline checking
    if (!validate_tta_message(signal) || MessageValidation::is_expired(signal)) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return false;
    }
    
    const bool success = signal_buffer_->try_write(signal);
    update_tta_metrics(success);
    
    return success;
}

bool SharedMemoryManager::consume_signal(TradingSignalMessage& signal) {
    if (is_producer_ || !signal_buffer_) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return false;
    }
    
    const bool success = signal_buffer_->try_read(signal);
    
    if (success) {
        // TTA deadline and validity checking
        if (!validate_tta_message(signal) || MessageValidation::is_expired(signal)) [[unlikely]] {
            update_tta_metrics(false);
            return false;
        }
    }
    
    update_tta_metrics(success);
    return success;
}

size_t SharedMemoryManager::consume_signal_batch(TradingSignalMessage* signals_array, size_t max_count) {
    if (is_producer_ || !signal_buffer_ || !signals_array) [[unlikely]] {
        update_tta_metrics(false);
        return 0;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    const size_t bounded_count = std::min(max_count, tta_config_.max_batch_size);
    
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return 0;
    }
    
    const size_t consumed = signal_buffer_->try_read_batch(signals_array, bounded_count);
    
    // TTA validation with deadline filtering
    size_t valid_count = 0;
    for (size_t i = 0; i < consumed; ++i) {
        if (validate_tta_message(signals_array[i]) && !MessageValidation::is_expired(signals_array[i])) {
            if (i != valid_count) {
                signals_array[valid_count] = signals_array[i];
            }
            ++valid_count;
        }
        // Continue processing all signals in batch (don't break like market data)
        // because signal filtering is expected behavior
    }
    
    update_tta_metrics(valid_count > 0);
    return valid_count;
}

// Control operations with TTA priority handling
bool SharedMemoryManager::publish_control(const ControlMessage& control) {
    // Control messages can be published by both producer and consumer for TTA coordination
    if (!control_buffer_) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    
    if (!validate_tta_message(control)) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return false;
    }
    
    const bool success = control_buffer_->try_write(control);
    update_tta_metrics(success);
    
    return success;
}

bool SharedMemoryManager::consume_control(ControlMessage& control) {
    // Control messages can be consumed by both producer and consumer for TTA coordination
    if (!control_buffer_) [[unlikely]] {
        update_tta_metrics(false);
        return false;
    }
    
    const auto start_time = Core::Timing::Clock::now();
    
    if (should_timeout(start_time)) [[unlikely]] {
        timeout_events_.fetch_add(1, std::memory_order_relaxed);
        update_tta_metrics(false);
        return false;
    }
    
    const bool success = control_buffer_->try_read(control);
    
    if (success) {
        if (!validate_tta_message(control)) [[unlikely]] {
            update_tta_metrics(false);
            return false;
        }
    }
    
    update_tta_metrics(success);
    return success;
}

// TTA status and monitoring implementation
SharedMemoryManager::TTABufferStatus SharedMemoryManager::get_tta_status() const {
    TTABufferStatus status{};
    
    // Update buffer health before reporting
    update_buffer_health();
    
    // Basic buffer information
    if (market_data_buffer_) {
        status.market_data_size = market_data_buffer_->size();
        status.market_data_utilization = market_data_buffer_->utilization();
        status.market_data_total_messages = market_data_buffer_->total_writes();
        status.market_data_healthy = buffer_health_status_[0];
    }
    
    if (signal_buffer_) {
        status.signal_size = signal_buffer_->size();
        status.signal_utilization = signal_buffer_->utilization();
        status.signal_total_messages = signal_buffer_->total_writes();
        status.signal_healthy = buffer_health_status_[1];
    }
    
    if (control_buffer_) {
        status.control_size = control_buffer_->size();
        status.control_utilization = control_buffer_->utilization();
        status.control_total_messages = control_buffer_->total_writes();
        status.control_healthy = buffer_health_status_[2];
    }
    
    // TTA-specific metrics
    status.total_operations = total_operations_.load(std::memory_order_relaxed);
    status.failed_operations = failed_operations_.load(std::memory_order_relaxed);
    status.timeout_events = timeout_events_.load(std::memory_order_relaxed);
    
    if (status.total_operations > 0) {
        status.operation_failure_rate = static_cast<double>(status.failed_operations) / 
                                       static_cast<double>(status.total_operations);
    } else {
        status.operation_failure_rate = 0.0;
    }
    
    // Overall TTA health assessment
    status.is_tta_healthy = status.market_data_healthy && 
                           status.signal_healthy && 
                           status.control_healthy &&
                           status.operation_failure_rate < 0.05;  // < 5% failure rate
    
    // TTA timing metrics (simplified for now)
    status.max_operation_latency = tta_config_.operation_timeout;
    status.avg_operation_latency = tta_config_.operation_timeout / 2;  // Estimate
    status.deadline_misses = status.timeout_events;
    
    return status;
}

bool SharedMemoryManager::is_tta_healthy() const {
    const auto now = Core::Timing::Clock::now();
    
    // Perform health check if interval has passed
    if (tta_config_.enable_automatic_health_checks && 
        now - last_health_check_ > tta_config_.health_check_interval) {
        return perform_tta_health_check();
    }
    
    // Quick health assessment based on cached status
    const uint64_t total_ops = total_operations_.load(std::memory_order_relaxed);
    const uint64_t failed_ops = failed_operations_.load(std::memory_order_relaxed);
    
    if (total_ops == 0) return true;  // No operations yet
    
    const double failure_rate = static_cast<double>(failed_ops) / static_cast<double>(total_ops);
    return failure_rate < 0.05;  // < 5% failure rate threshold
}

bool SharedMemoryManager::perform_tta_health_check() const {
    last_health_check_ = Core::Timing::Clock::now();
    
    bool overall_health = true;
    
    // Check individual buffer health
    if (market_data_buffer_) {
        buffer_health_status_[0] = market_data_buffer_->is_tta_healthy();
        overall_health &= buffer_health_status_[0];
    }
    
    if (signal_buffer_) {
        buffer_health_status_[1] = signal_buffer_->is_tta_healthy();
        overall_health &= buffer_health_status_[1];
    }
    
    if (control_buffer_) {
        buffer_health_status_[2] = control_buffer_->is_tta_healthy();
        overall_health &= buffer_health_status_[2];
    }
    
    // Check operation failure rate
    const uint64_t total_ops = total_operations_.load(std::memory_order_relaxed);
    const uint64_t failed_ops = failed_operations_.load(std::memory_order_relaxed);
    
    if (total_ops > 100) {  // Only check failure rate after sufficient operations
        const double failure_rate = static_cast<double>(failed_ops) / static_cast<double>(total_ops);
        overall_health &= (failure_rate < 0.05);
    }
    
    return overall_health;
}

void SharedMemoryManager::reset_tta_metrics() {
    total_operations_.store(0, std::memory_order_relaxed);
    failed_operations_.store(0, std::memory_order_relaxed);
    timeout_events_.store(0, std::memory_order_relaxed);
    
    if (market_data_buffer_) market_data_buffer_->reset_tta_metrics();
    if (signal_buffer_) signal_buffer_->reset_tta_metrics();
    if (control_buffer_) control_buffer_->reset_tta_metrics();
    
    last_health_check_ = Core::Timing::Clock::now();
    buffer_health_status_.fill(true);
}

void SharedMemoryManager::configure_tta_parameters(const TTAConfig& config) {
    tta_config_ = config;
}

// Legacy interface implementation
SharedMemoryManager::BufferStatus SharedMemoryManager::get_status() const {
    const auto tta_status = get_tta_status();
    
    BufferStatus legacy_status;
    legacy_status.market_data_size = tta_status.market_data_size;
    legacy_status.signal_size = tta_status.signal_size;
    legacy_status.control_size = tta_status.control_size;
    legacy_status.market_data_utilization = tta_status.market_data_utilization;
    legacy_status.signal_utilization = tta_status.signal_utilization;
    legacy_status.control_utilization = tta_status.control_utilization;
    legacy_status.market_data_total_messages = tta_status.market_data_total_messages;
    legacy_status.signal_total_messages = tta_status.signal_total_messages;
    legacy_status.control_total_messages = tta_status.control_total_messages;
    
    return legacy_status;
}

bool SharedMemoryManager::is_healthy() const {
    return is_tta_healthy();
}

void SharedMemoryManager::clear_all_buffers() {
    // TTA warning: This operation can disrupt deterministic timing
    MarketDataMessage market_msg;
    TradingSignalMessage signal_msg;
    ControlMessage control_msg;
    
    if (market_data_buffer_) {
        while (market_data_buffer_->try_read(market_msg)) {
            // Drain buffer
        }
    }
    
    if (signal_buffer_) {
        while (signal_buffer_->try_read(signal_msg)) {
            // Drain buffer
        }
    }
    
    if (control_buffer_) {
        while (control_buffer_->try_read(control_msg)) {
            // Drain buffer
        }
    }
}

// Private helper methods
void SharedMemoryManager::update_tta_metrics(bool success) const {
    total_operations_.fetch_add(1, std::memory_order_relaxed);
    if (!success) {
        failed_operations_.fetch_add(1, std::memory_order_relaxed);
    }
}

template<typename MessageType>
bool SharedMemoryManager::validate_tta_message(const MessageType& msg) const {
    // Call the free function from the Alaris::IPC namespace
    return Alaris::IPC::validate_tta_message(msg);
}

bool SharedMemoryManager::should_timeout(Core::Timing::TimePoint start_time) const {
    if (!tta_config_.enable_performance_monitoring) return false;
    
    const auto elapsed = Core::Timing::Clock::now() - start_time;
    return elapsed > tta_config_.operation_timeout;
}

void SharedMemoryManager::update_buffer_health() const {
    const auto now = Core::Timing::Clock::now();
    
    // Only update health status periodically to avoid overhead
    if (now - last_buffer_health_update_ < tta_config_.health_check_interval) {
        return;
    }
    
    last_buffer_health_update_ = now;
    
    if (market_data_buffer_) {
        buffer_health_status_[0] = market_data_buffer_->is_tta_healthy();
    }
    
    if (signal_buffer_) {
        buffer_health_status_[1] = signal_buffer_->is_tta_healthy();
    }
    
    if (control_buffer_) {
        buffer_health_status_[2] = control_buffer_->is_tta_healthy();
    }
}

void SharedMemoryManager::report_tta_error(const std::string& operation, const std::string& error) const {
    // TTA-safe error reporting with bounded execution time
    fprintf(stderr, "TTA SharedMemoryManager Error [PID %u, Op: %s]: %s\n", 
            process_id_, operation.c_str(), error.c_str());
}

} // namespace Alaris::IPC