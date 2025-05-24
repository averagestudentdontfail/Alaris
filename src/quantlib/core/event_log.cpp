// src/quantlib/core/event_log.cpp
#include "event_log.h"
#include <cstring>
#include <thread>
#include <iostream>
#include <sstream>

namespace Alaris::Core {

EventLogger::EventLogger(const std::string& filename, bool binary_mode)
    : filename_(filename), binary_mode_(binary_mode), sequence_number_(0),
      total_events_logged_(0), total_bytes_written_(0) {
    
    auto flags = std::ios::out;
    if (binary_mode_) {
        flags |= std::ios::binary;
    }
    
    log_file_.open(filename_, flags);
    if (!log_file_.is_open()) {
        throw std::runtime_error("Failed to open event log file: " + filename_);
    }
    
    // Write file header
    if (binary_mode_) {
        const char* header = "ALARIS_EVENT_LOG_V1.0";
        log_file_.write(header, strlen(header));
    } else {
        log_file_ << "# Alaris Event Log V1.0\n";
        log_file_ << "# Timestamp,Sequence,EventType,Data\n";
    }
}

EventLogger::~EventLogger() {
    if (log_file_.is_open()) {
        flush();
        log_file_.close();
    }
}

uint32_t EventLogger::calculate_checksum(const void* data, size_t size) const {
    // Simple CRC32-like checksum
    uint32_t checksum = 0xFFFFFFFF;
    const uint8_t* bytes = static_cast<const uint8_t*>(data);
    
    for (size_t i = 0; i < size; ++i) {
        checksum ^= bytes[i];
        for (int j = 0; j < 8; ++j) {
            if (checksum & 1) {
                checksum = (checksum >> 1) ^ 0xEDB88320;
            } else {
                checksum = checksum >> 1;
            }
        }
    }
    
    return checksum ^ 0xFFFFFFFF;
}

void EventLogger::write_event(EventType type, const void* data, size_t size) {
    std::lock_guard<std::mutex> lock(log_mutex_);
    
    auto now = std::chrono::high_resolution_clock::now();
    uint64_t timestamp = std::chrono::duration_cast<std::chrono::nanoseconds>(
        now.time_since_epoch()).count();
    
    uint64_t seq = sequence_number_.fetch_add(1);
    
    if (binary_mode_) {
        EventHeader header;
        header.timestamp = timestamp;
        header.sequence_number = seq;
        header.event_type = type;
        header.data_size = static_cast<uint32_t>(size);
        header.checksum = calculate_checksum(data, size);
        
        log_file_.write(reinterpret_cast<const char*>(&header), sizeof(header));
        log_file_.write(static_cast<const char*>(data), size);
    } else {
        log_file_ << timestamp << "," << seq << "," << static_cast<uint32_t>(type) << ",";
        
        // Write data as hex
        const uint8_t* bytes = static_cast<const uint8_t*>(data);
        for (size_t i = 0; i < size; ++i) {
            log_file_ << std::hex << static_cast<int>(bytes[i]);
        }
        log_file_ << std::dec << "\n";
    }
    
    total_events_logged_++;
    total_bytes_written_ += sizeof(EventHeader) + size;
}

void EventLogger::log_market_data(const IPC::MarketDataMessage& data) {
    write_event(EventType::MARKET_DATA, &data, sizeof(data));
}

void EventLogger::log_trading_signal(const IPC::TradingSignalMessage& signal) {
    write_event(EventType::TRADING_SIGNAL, &signal, sizeof(signal));
}

void EventLogger::log_control_message(const IPC::ControlMessage& control) {
    write_event(EventType::SYSTEM_STATUS, &control, sizeof(control));
}

void EventLogger::log_pricing_request(uint32_t symbol_id, double underlying, 
                                     double strike, double volatility, double time_to_expiry) {
    struct PricingRequestEvent {
        uint32_t symbol_id;
        double underlying;
        double strike;
        double volatility;
        double time_to_expiry;
    } event = {symbol_id, underlying, strike, volatility, time_to_expiry};
    
    write_event(EventType::PRICING_REQUEST, &event, sizeof(event));
}

void EventLogger::log_pricing_result(uint32_t symbol_id, double price, 
                                    double delta, double gamma, double vega, double theta) {
    struct PricingResultEvent {
        uint32_t symbol_id;
        double price;
        double delta;
        double gamma;
        double vega;
        double theta;
    } event = {symbol_id, price, delta, gamma, vega, theta};
    
    write_event(EventType::PRICING_RESULT, &event, sizeof(event));
}

void EventLogger::log_volatility_update(uint32_t symbol_id, double forecast_vol,
                                       double implied_vol, double confidence) {
    struct VolatilityUpdateEvent {
        uint32_t symbol_id;
        double forecast_vol;
        double implied_vol;
        double confidence;
    } event = {symbol_id, forecast_vol, implied_vol, confidence};
    
    write_event(EventType::VOLATILITY_UPDATE, &event, sizeof(event));
}

void EventLogger::log_system_status(const std::string& message) {
    write_event(EventType::SYSTEM_STATUS, message.c_str(), message.length() + 1);
}

void EventLogger::log_error(const std::string& error_message) {
    write_event(EventType::ERROR_EVENT, error_message.c_str(), error_message.length() + 1);
}

void EventLogger::log_performance_metric(const std::string& metric_name, double value) {
    struct PerformanceMetricEvent {
        char name[64];
        double value;
    } event;
    
    strncpy(event.name, metric_name.c_str(), sizeof(event.name) - 1);
    event.name[sizeof(event.name) - 1] = '\0';
    event.value = value;
    
    write_event(EventType::PERFORMANCE_METRIC, &event, sizeof(event));
}

void EventLogger::flush() {
    std::lock_guard<std::mutex> lock(log_mutex_);
    log_file_.flush();
}

bool EventLogger::is_healthy() const {
    return log_file_.is_open() && log_file_.good();
}

} // namespace Alaris::Core