#pragma once

#include "../ipc/message_types.h" // For Alaris::IPC message structures
#include <fstream>
#include <string>
#include <vector> // For EventReplayEngine data buffer
#include <chrono>    // For timestamps
#include <functional> // For EventCallback
#include <atomic>     // For sequence_number_ and replay engine state
#include <mutex>      // For log_mutex_

namespace Alaris::Core {

/**
 * @brief Defines the types of events that can be logged.
 */
enum class EventType : uint32_t {
    UNKNOWN = 0,
    MARKET_DATA_UPDATE = 1,      // IPC::MarketDataMessage
    TRADING_SIGNAL_GENERATED = 2,// IPC::TradingSignalMessage
    CONTROL_MESSAGE_RECEIVED = 3,// IPC::ControlMessage
    STRATEGY_PARAMETER_CHANGE = 4,
    VOLATILITY_MODEL_UPDATE = 5, // e.g., GJR-GARCH model parameters or forecast
    PRICING_ENGINE_REQUEST = 6,  // If logging individual pricing calls
    PRICING_ENGINE_RESULT = 7,
    ORDER_EVENT_FROM_EXCHANGE = 8, // If QuantLib process handled order status
    SYSTEM_STATUS_CHANGE = 9,   // e.g., "TradingEnabled", "ComponentX Started"
    PERFORMANCE_METRIC_LOG = 10, // For logging arbitrary performance data
    ERROR_LOG = 11,
    WARNING_LOG = 12,
    DEBUG_LOG = 13, // Generic debug messages
    CUSTOM_STRATEGY_EVENT = 100 // Base for strategy-specific events
};

/**
 * @brief Header structure prepended to each event in a binary log file.
 */
struct EventHeader {
    uint64_t timestamp_ns;        // Nanosecond precision timestamp (e.g., from std::chrono)
    uint64_t sequence_number;     // Monotonically increasing sequence number for this logger instance
    EventType event_type;         // Type of the event
    uint32_t data_size_bytes;     // Size of the event data payload that follows this header
    uint32_t data_checksum;       // Checksum of the event data payload (e.g., CRC32)
};

/**
 * @brief Handles writing events to a log file, in binary or text format.
 * This class is designed to be thread-safe for concurrent logging calls.
 */
class EventLogger {
private:
    std::ofstream log_file_stream_;
    mutable std::mutex log_mutex_; // Protects access to log_file_stream_ and internal counters
    std::atomic<uint64_t> current_sequence_number_;
    std::string log_filename_;
    bool use_binary_format_; // True for binary logging, false for human-readable text

    // Performance/statistics tracking
    std::atomic<uint64_t> total_events_logged_count_;
    std::atomic<uint64_t> total_bytes_written_count_;

    // Internal helper to calculate checksum
    uint32_t calculate_data_checksum(const void* data, size_t size) const;
    // Internal helper to write event header and data
    void write_log_entry(EventType type, const void* data_payload, size_t payload_size);

public:
    /**
     * @brief Constructs an EventLogger.
     * @param filename The path to the log file.
     * @param binary_mode If true, logs in a compact binary format. Otherwise, logs in human-readable text.
     * @throws std::runtime_error if the log file cannot be opened.
     */
    explicit EventLogger(const std::string& filename, bool binary_mode = true);
    ~EventLogger();

    // Non-copyable
    EventLogger(const EventLogger&) = delete;
    EventLogger& operator=(const EventLogger&) = delete;

    // --- Specific Event Logging Methods ---
    void log_market_data(const IPC::MarketDataMessage& data_msg);
    void log_trading_signal(const IPC::TradingSignalMessage& signal_msg);
    void log_control_message(const IPC::ControlMessage& control_msg);
    
    // --- Generic Event Logging Methods ---
    void log_system_status(const std::string& status_message);
    void log_error_message(const std::string& error_msg);
    void log_warning_message(const std::string& warning_msg);
    void log_debug_message(const std::string& debug_msg);
    void log_performance_metric(const std::string& metric_name, double metric_value);
    void log_custom_event(EventType custom_type, const std::string& event_details); // For text based custom
    void log_custom_binary_event(EventType custom_type, const void* data_payload, size_t payload_size);


    // --- File Management ---
    /**
     * @brief Flushes any buffered log entries to the file.
     */
    void flush_log();

    /**
     * @brief Closes the current log file and opens a new one.
     * Useful for log rotation.
     * @param new_filename The path to the new log file.
     * @throws std::runtime_error if the new log file cannot be opened.
     */
    void rotate_log_file(const std::string& new_filename);

    // --- Statistics ---
    uint64_t get_total_events_logged() const { return total_events_logged_count_.load(); }
    uint64_t get_total_bytes_written() const { return total_bytes_written_count_.load(); }
    
    /**
     * @brief Checks if the logger is in a healthy state (e.g., file stream is good).
     * @return True if healthy, false otherwise.
     */
    bool is_healthy() const;
};


/**
 * @brief Callback function type for the EventReplayEngine.
 * Parameters: EventType, pointer to event data, size of event data.
 */
using EventReplayCallback = std::function<void(const EventHeader& header, const std::vector<std::byte>& data_buffer)>;

/**
 * @brief Engine to replay events from a log file created by EventLogger.
 * Supports replaying events at various speeds or step-by-step.
 */
class EventReplayEngine {
private:
    std::ifstream log_file_stream_;
    EventReplayCallback event_callback_;
    std::atomic<bool> is_replaying_;
    std::atomic<bool> is_paused_;
    double replay_speed_factor_; // 1.0 for real-time, <1.0 slower, >1.0 faster
    
    uint64_t current_replay_sequence_number_;
    TimePoint last_event_original_timestamp_; // To manage replay speed

    // Internal helper to read and validate one event
    bool read_next_event(EventHeader& out_header, std::vector<std::byte>& out_data_buffer);
    bool validate_event_checksum(const EventHeader& header, const std::vector<std::byte>& data_buffer) const;
    uint32_t calculate_data_checksum(const void* data, size_t size) const; // Must match EventLogger's checksum

    std::thread replay_thread_; // Replay often runs in its own thread
    void replay_loop(uint64_t start_sequence_num);

public:
    /**
     * @brief Constructs an EventReplayEngine.
     * @param log_filename The path to the binary log file to replay.
     * @param callback The function to call for each replayed event.
     * @throws std::runtime_error if the log file cannot be opened.
     */
    explicit EventReplayEngine(const std::string& log_filename, EventReplayCallback callback);
    ~EventReplayEngine();

    // Non-copyable
    EventReplayEngine(const EventReplayEngine&) = delete;
    EventReplayEngine& operator=(const EventReplayEngine&) = delete;

    // --- Replay Control ---
    /**
     * @brief Starts replaying events from a given sequence number.
     * This method may start a new thread for the replay loop.
     * @param start_sequence_number The sequence number to start replaying from (0 for beginning).
     */
    void start_replay(uint64_t start_sequence_number = 0);

    void pause_replay();
    void resume_replay();
    
    /**
     * @brief Stops the replay process. If a replay thread is active, it will be joined.
     */
    void stop_replay();

    // --- Configuration ---
    /**
     * @brief Sets the replay speed.
     * @param speed_factor 1.0 for real-time based on original timestamps,
     * <1.0 for slower, >1.0 for faster. 0 for step-through (manual advance).
     */
    void set_replay_speed(double speed_factor);
    
    // --- Status ---
    bool is_replaying() const { return is_replaying_.load(); }
    bool is_paused() const { return is_paused_.load(); }
    uint64_t get_current_replay_sequence() const { return current_replay_sequence_number_; }
    bool is_eof() const; // Checks if log file EOF is reached
};

} // namespace Alaris::Core