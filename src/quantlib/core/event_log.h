#pragma once

#include "../ipc/message_types.h"
#include "time_trigger.h"
#include <fstream>
#include <string>
#include <vector>
#include <chrono>
#include <functional>
#include <atomic>
#include <mutex>
#include <thread>

namespace Alaris::Core {

enum class EventType : uint32_t {
    UNKNOWN = 0,
    MARKET_DATA_UPDATE = 1,
    TRADING_SIGNAL_GENERATED = 2,
    CONTROL_MESSAGE_RECEIVED = 3,
    STRATEGY_PARAMETER_CHANGE = 4,
    VOLATILITY_MODEL_UPDATE = 5,
    PRICING_ENGINE_REQUEST = 6,
    PRICING_ENGINE_RESULT = 7,
    ORDER_EVENT_FROM_EXCHANGE = 8,
    SYSTEM_STATUS_CHANGE = 9,
    PERFORMANCE_METRIC_LOG = 10,
    ERROR_LOG = 11,
    WARNING_LOG = 12,
    INFO_LOG = 13,
    DEBUG_LOG = 14,
    CUSTOM_STRATEGY_EVENT = 100
};

struct EventHeader {
    uint64_t timestamp_ns;
    uint64_t sequence_number;
    EventType event_type;
    uint32_t data_size_bytes;
    uint32_t data_checksum;
};

class EventLogger {
private:
    std::ofstream log_file_stream_;
    mutable std::mutex log_mutex_;
    std::atomic<uint64_t> current_sequence_number_;
    std::string log_filename_;
    bool use_binary_format_;

    std::atomic<uint64_t> total_events_logged_count_;
    std::atomic<uint64_t> total_bytes_written_count_;

    uint32_t calculate_data_checksum(const void* data, size_t size) const;
    void write_log_entry(EventType type, const void* data_payload, size_t payload_size);

public:
    explicit EventLogger(const std::string& filename, bool binary_mode = true);
    ~EventLogger();

    EventLogger(const EventLogger&) = delete;
    EventLogger& operator=(const EventLogger&) = delete;

    // Market data and trading
    void log_market_data(const IPC::MarketDataMessage& data_msg);
    void log_trading_signal(const IPC::TradingSignalMessage& signal_msg);
    void log_control_message(const IPC::ControlMessage& control_msg);
    
    // System and status logging
    void log_system_status(const std::string& status_message);
    void log_performance_metric(const std::string& metric_name, double metric_value);
    
    // Error and debug logging - FIXED: Added these methods
    void log_error(const std::string& error_msg);
    void log_warning(const std::string& warning_msg);
    void log_info(const std::string& info_msg);
    void log_debug(const std::string& debug_msg);
    
    // Custom events
    void log_custom_event(EventType custom_type, const std::string& event_details);
    void log_custom_binary_event(EventType custom_type, const void* data_payload, size_t payload_size);

    // File management
    void flush_log();
    void rotate_log_file(const std::string& new_filename);

    // Metrics and health
    uint64_t get_total_events_logged() const { return total_events_logged_count_.load(); }
    uint64_t get_total_bytes_written() const { return total_bytes_written_count_.load(); }
    bool is_healthy() const;
};

// TimePoint and Duration definitions for consistency
using TimePoint = Alaris::Core::TimeTriggeredExecutor::Clock::time_point;
using Duration = Alaris::Core::TimeTriggeredExecutor::Clock::duration;

using EventReplayCallback = std::function<void(const EventHeader& header, const std::vector<std::byte>& data_buffer)>;

class EventReplayEngine {
private:
    std::ifstream log_file_stream_;
    EventReplayCallback event_callback_;
    std::atomic<bool> is_replaying_;
    std::atomic<bool> is_paused_;
    double replay_speed_factor_;
    
    uint64_t current_replay_sequence_number_;
    TimePoint last_event_original_timestamp_;
    TimePoint replay_session_start_host_time_;
    Duration replay_session_first_event_original_offset_ns_;

    bool read_next_event(EventHeader& out_header, std::vector<std::byte>& out_data_buffer);
    bool validate_event_checksum(const EventHeader& header, const std::vector<std::byte>& data_buffer) const;
    uint32_t calculate_data_checksum(const void* data, size_t size) const;

    std::thread replay_thread_;
    void replay_loop(uint64_t start_sequence_num);

public:
    explicit EventReplayEngine(const std::string& log_filename, EventReplayCallback callback);
    ~EventReplayEngine();

    EventReplayEngine(const EventReplayEngine&) = delete;
    EventReplayEngine& operator=(const EventReplayEngine&) = delete;

    void start_replay(uint64_t start_sequence_number = 0);
    void pause_replay();
    void resume_replay();
    void stop_replay();
    void set_replay_speed(double speed_factor);
    
    bool is_replaying() const { return is_replaying_.load(std::memory_order_relaxed); }
    bool is_paused() const { return is_paused_.load(std::memory_order_relaxed); }
    uint64_t get_current_replay_sequence() const { return current_replay_sequence_number_; }
    bool is_eof() const;
};

} // namespace Alaris::Core