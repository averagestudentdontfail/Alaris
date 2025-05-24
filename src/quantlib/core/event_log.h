// src/quantlib/core/event_log.h
#pragma once

#include "../ipc/message_types.h"
#include <fstream>
#include <mutex>
#include <string>
#include <chrono>
#include <functional>

namespace Alaris::Core {

enum class EventType : uint32_t {
    MARKET_DATA = 1,
    PRICING_REQUEST = 2,
    PRICING_RESULT = 3,
    VOLATILITY_UPDATE = 4,
    TRADING_SIGNAL = 5,
    ORDER_EVENT = 6,
    SYSTEM_STATUS = 7,
    PERFORMANCE_METRIC = 8,
    ERROR_EVENT = 9
};

struct EventHeader {
    uint64_t timestamp;
    uint64_t sequence_number;
    EventType event_type;
    uint32_t data_size;
    uint32_t checksum;
};

class EventLogger {
private:
    std::ofstream log_file_;
    mutable std::mutex log_mutex_;
    std::atomic<uint64_t> sequence_number_;
    std::string filename_;
    bool binary_mode_;
    
    // Performance tracking
    mutable uint64_t total_events_logged_;
    mutable uint64_t total_bytes_written_;
    
    uint32_t calculate_checksum(const void* data, size_t size) const;
    void write_event(EventType type, const void* data, size_t size);
    
public:
    explicit EventLogger(const std::string& filename, bool binary_mode = true);
    ~EventLogger();
    
    // Non-copyable
    EventLogger(const EventLogger&) = delete;
    EventLogger& operator=(const EventLogger&) = delete;
    
    // Event logging methods
    void log_market_data(const IPC::MarketDataMessage& data);
    void log_trading_signal(const IPC::TradingSignalMessage& signal);
    void log_control_message(const IPC::ControlMessage& control);
    
    // Custom event logging
    void log_pricing_request(uint32_t symbol_id, double underlying, double strike,
                           double volatility, double time_to_expiry);
    void log_pricing_result(uint32_t symbol_id, double price, double delta, 
                          double gamma, double vega, double theta);
    void log_volatility_update(uint32_t symbol_id, double forecast_vol, 
                             double implied_vol, double confidence);
    void log_system_status(const std::string& message);
    void log_error(const std::string& error_message);
    
    // Performance logging
    void log_performance_metric(const std::string& metric_name, double value);
    
    // File management
    void flush();
    void rotate_log(const std::string& new_filename);
    
    // Statistics
    uint64_t total_events() const { return total_events_logged_; }
    uint64_t total_bytes() const { return total_bytes_written_; }
    bool is_healthy() const;
};

using EventCallback = std::function<void(EventType, const void*, size_t)>;

class EventReplayEngine {
private:
    std::ifstream log_file_;
    EventCallback callback_;
    std::atomic<bool> running_;
    std::atomic<bool> paused_;
    double replay_speed_;
    uint64_t start_sequence_;
    uint64_t current_sequence_;
    
    bool read_event(EventHeader& header, std::vector<uint8_t>& data);
    bool validate_event(const EventHeader& header, const void* data) const;
    
public:
    explicit EventReplayEngine(const std::string& filename, EventCallback callback);
    ~EventReplayEngine();
    
    // Non-copyable
    EventReplayEngine(const EventReplayEngine&) = delete;
    EventReplayEngine& operator=(const EventReplayEngine&) = delete;
    
    // Replay control
    void start_replay(uint64_t start_sequence = 0);
    void pause_replay();
    void resume_replay();
    void stop_replay();
    
    // Configuration
    void set_replay_speed(double speed_factor);
    void set_start_sequence(uint64_t sequence);
    
    // Status
    bool is_running() const { return running_; }
    bool is_paused() const { return paused_; }
    uint64_t current_sequence() const { return current_sequence_; }
};

} // namespace Alaris::Core