#include "event_log.h"
#include <stdexcept> // For std::runtime_error
#include <iomanip>   // For std::setw, std::setfill, std::hex for text logging
#include <cstring>   // For std::memcpy, std::strlen
#include <thread>    // For std::this_thread::sleep_for in replay engine
#include <vector>    // For EventReplayEngine data buffer

// For checksum, if using a library, include it here. Otherwise, implement.
// Basic CRC32-like checksum (example)
uint32_t simple_crc32_like(const void* data, size_t size, uint32_t initial_crc = 0xFFFFFFFF) {
    uint32_t crc = initial_crc;
    const unsigned char* buf = static_cast<const unsigned char*>(data);
    for (size_t i = 0; i < size; ++i) {
        crc ^= buf[i];
        for (int j = 0; j < 8; ++j) {
            crc = (crc & 1) ? ((crc >> 1) ^ 0xEDB88320) : (crc >> 1);
        }
    }
    return crc ^ 0xFFFFFFFF;
}


namespace Alaris::Core {

// --- EventLogger Implementation ---

EventLogger::EventLogger(const std::string& filename, bool binary_mode)
    : current_sequence_number_(0),
      log_filename_(filename),
      use_binary_format_(binary_mode),
      total_events_logged_count_(0),
      total_bytes_written_count_(0) {
    
    std::ios_base::openmode open_flags = std::ios::out | std::ios::app; // Append by default
    if (use_binary_format_) {
        open_flags |= std::ios::binary;
    }
    
    log_file_stream_.open(log_filename_, open_flags);
    if (!log_file_stream_.is_open()) {
        throw std::runtime_error("EventLogger: Failed to open log file: " + log_filename_);
    }

    // Optionally write a file header if the file is new/empty
    log_file_stream_.seekp(0, std::ios::end);
    if (log_file_stream_.tellp() == 0) { // File is empty
        if (use_binary_format_) {
            // Example binary file magic/version
            const char binary_header_magic[] = "ALARISLOG_V1B"; // 13 chars + null
            log_file_stream_.write(binary_header_magic, sizeof(binary_header_magic) -1);
            total_bytes_written_count_ += (sizeof(binary_header_magic) -1);
        } else {
            log_file_stream_ << "# Alaris Event Log V1.0 (Text Format)\n";
            log_file_stream_ << "# Timestamp(ns),Sequence,EventType,DataSize,Checksum,Payload(HexIfBinary)\n";
            total_bytes_written_count_ += std::string("# Alaris Event Log V1.0 (Text Format)\n# Timestamp(ns),Sequence,EventType,DataSize,Checksum,Payload(HexIfBinary)\n").length();
        }
        log_file_stream_.flush(); // Ensure header is written
    }
    // std::cout << "EventLogger initialized for file: " << log_filename_ << (use_binary_format_ ? " (Binary)" : " (Text)") << std::endl;
}

EventLogger::~EventLogger() {
    if (log_file_stream_.is_open()) {
        flush_log();
        log_file_stream_.close();
    }
    // std::cout << "EventLogger for " << log_filename_ << " destroyed. Events: " << total_events_logged_count_ << ", Bytes: " << total_bytes_written_count_ << std::endl;
}

uint32_t EventLogger::calculate_data_checksum(const void* data, size_t size) const {
    return simple_crc32_like(data, size); // Use the chosen checksum function
}

void EventLogger::write_log_entry(EventType type, const void* data_payload, size_t payload_size) {
    EventHeader header;
    auto now = std::chrono::high_resolution_clock::now();
    header.timestamp_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(now.time_since_epoch()).count();
    header.sequence_number = current_sequence_number_++;
    header.event_type = type;
    header.data_size_bytes = static_cast<uint32_t>(payload_size);
    header.data_checksum = calculate_data_checksum(data_payload, payload_size);

    size_t bytes_written_this_event = 0;

    std::lock_guard<std::mutex> lock(log_mutex_);
    if (!log_file_stream_.is_open() || !log_file_stream_.good()) {
        // Handle error: log file not available (e.g., try reopening, log to stderr)
        std::cerr << "EventLogger: Log file '" << log_filename_ << "' is not open or in a bad state. Event lost." << std::endl;
        return;
    }

    if (use_binary_format_) {
        log_file_stream_.write(reinterpret_cast<const char*>(&header), sizeof(EventHeader));
        if (payload_size > 0 && data_payload) {
            log_file_stream_.write(static_cast<const char*>(data_payload), payload_size);
        }
        bytes_written_this_event = sizeof(EventHeader) + payload_size;
    } else {
        // Text format: Timestamp,Sequence,EventType,DataSize,Checksum,PayloadAsHex
        std::ostringstream oss;
        oss << header.timestamp_ns << ","
            << header.sequence_number << ","
            << static_cast<uint32_t>(header.event_type) << ","
            << header.data_size_bytes << ","
            << header.data_checksum << ",";
        
        if (payload_size > 0 && data_payload) {
            const unsigned char* char_data = static_cast<const unsigned char*>(data_payload);
            oss << std::hex << std::setfill('0');
            for (size_t i = 0; i < payload_size; ++i) {
                oss << std::setw(2) << static_cast<int>(char_data[i]);
            }
            oss << std::dec; // Reset hex mode
        }
        oss << "\n";
        std::string line = oss.str();
        log_file_stream_ << line;
        bytes_written_this_event = line.length();
    }
    
    log_file_stream_.flush(); // Flush after each event for critical logging, or batch for performance

    total_events_logged_count_++;
    total_bytes_written_count_ += bytes_written_this_event;
}

void EventLogger::log_market_data(const IPC::MarketDataMessage& data_msg) {
    write_log_entry(EventType::MARKET_DATA_UPDATE, &data_msg, sizeof(data_msg));
}

void EventLogger::log_trading_signal(const IPC::TradingSignalMessage& signal_msg) {
    write_log_entry(EventType::TRADING_SIGNAL_GENERATED, &signal_msg, sizeof(signal_msg));
}

void EventLogger::log_control_message(const IPC::ControlMessage& control_msg) {
    write_log_entry(EventType::CONTROL_MESSAGE_RECEIVED, &control_msg, sizeof(control_msg));
}

void EventLogger::log_system_status(const std::string& status_message) {
    write_log_entry(EventType::SYSTEM_STATUS_CHANGE, status_message.c_str(), status_message.length());
}

void EventLogger::log_error_message(const std::string& error_msg) {
    write_log_entry(EventType::ERROR_LOG, error_msg.c_str(), error_msg.length());
}

void EventLogger::log_warning_message(const std::string& warning_msg) {
    write_log_entry(EventType::WARNING_LOG, warning_msg.c_str(), warning_msg.length());
}
void EventLogger::log_debug_message(const std::string& debug_msg) {
    write_log_entry(EventType::DEBUG_LOG, debug_msg.c_str(), debug_msg.length());
}

void EventLogger::log_performance_metric(const std::string& metric_name, double metric_value) {
    // For text logging, can make this more readable. For binary, need a struct.
    // Example: "metric_name=value"
    std::string payload = metric_name + "=" + std::to_string(metric_value);
    write_log_entry(EventType::PERFORMANCE_METRIC_LOG, payload.c_str(), payload.length());
}

void EventLogger::log_custom_event(EventType custom_type, const std::string& event_details) {
    write_log_entry(custom_type, event_details.c_str(), event_details.length());
}

void EventLogger::log_custom_binary_event(EventType custom_type, const void* data_payload, size_t payload_size) {
    write_log_entry(custom_type, data_payload, payload_size);
}


void EventLogger::flush_log() {
    std::lock_guard<std::mutex> lock(log_mutex_);
    if (log_file_stream_.is_open()) {
        log_file_stream_.flush();
    }
}

void EventLogger::rotate_log_file(const std::string& new_filename) {
    std::lock_guard<std::mutex> lock(log_mutex_);
    if (log_file_stream_.is_open()) {
        log_file_stream_.flush();
        log_file_stream_.close();
    }
    log_filename_ = new_filename; // Update filename member
    std::ios_base::openmode open_flags = std::ios::out | std::ios::app; // Append by default
    if (use_binary_format_) {
        open_flags |= std::ios::binary;
    }
    log_file_stream_.open(log_filename_, open_flags);
    if (!log_file_stream_.is_open()) {
        throw std::runtime_error("EventLogger: Failed to open new log file for rotation: " + log_filename_);
    }
    // Optionally write a header to the new rotated file if it's empty
    log_file_stream_.seekp(0, std::ios::end);
    if (log_file_stream_.tellp() == 0) {
         if (use_binary_format_) {
            const char binary_header_magic[] = "ALARISLOG_V1B";
            log_file_stream_.write(binary_header_magic, sizeof(binary_header_magic) -1);
        } else {
            log_file_stream_ << "# Alaris Event Log V1.0 (Text Format)\n";
            log_file_stream_ << "# Timestamp(ns),Sequence,EventType,DataSize,Checksum,Payload(HexIfBinary)\n";
        }
        log_file_stream_.flush();
    }
}

bool EventLogger::is_healthy() const {
    std::lock_guard<std::mutex> lock(log_mutex_); // Good practice even for read-only check on stream state
    return log_file_stream_.is_open() && log_file_stream_.good();
}


// --- EventReplayEngine Implementation (Skeleton) ---

EventReplayEngine::EventReplayEngine(const std::string& log_filename, EventReplayCallback callback)
    : event_callback_(std::move(callback)),
      is_replaying_(false),
      is_paused_(false),
      replay_speed_factor_(1.0),
      current_replay_sequence_number_(0),
      last_event_original_timestamp_(TimePoint::min()) {

    std::ios_base::openmode open_flags = std::ios::in;
    // Assuming logs to replay are always binary for now, as text replay is more complex
    open_flags |= std::ios::binary; 
    
    log_file_stream_.open(log_filename, open_flags);
    if (!log_file_stream_.is_open()) {
        throw std::runtime_error("EventReplayEngine: Failed to open log file: " + log_filename);
    }

    // Read and validate file header if one exists (e.g., "ALARISLOG_V1B")
    char binary_header_magic_check[13]; // Size of "ALARISLOG_V1B"
    log_file_stream_.read(binary_header_magic_check, sizeof(binary_header_magic_check));
    if (!log_file_stream_ || std::string(binary_header_magic_check, sizeof(binary_header_magic_check)) != "ALARISLOG_V1B") {
        // If not binary or no header, seek back to beginning for text or headerless binary
        log_file_stream_.clear(); // Clear error flags
        log_file_stream_.seekg(0, std::ios::beg); 
        // Could attempt to check for text header here if needed
        // For now, assume binary logs have the magic. If not, it's an issue.
        // Or, if the user intends to replay text logs, this part needs modification.
        // This simplified version will just proceed, but might fail on text logs.
        // Let's throw if the specific binary header is expected and not found.
        // throw std::runtime_error("EventReplayEngine: Log file does not appear to be a valid Alaris binary log.");
        std::cout << "EventReplayEngine: Warning - Binary log header not found or doesn't match. Assuming headerless or text log." << std::endl;
        log_file_stream_.clear();
        log_file_stream_.seekg(0, std::ios::beg);
    }
}

EventReplayEngine::~EventReplayEngine() {
    stop_replay(); // Ensure thread is joined
    if (log_file_stream_.is_open()) {
        log_file_stream_.close();
    }
}

uint32_t EventReplayEngine::calculate_data_checksum(const void* data, size_t size) const {
    return simple_crc32_like(data, size); // Must match EventLogger's implementation
}

bool EventReplayEngine::validate_event_checksum(const EventHeader& header, const std::vector<std::byte>& data_buffer) const {
    if (header.data_size_bytes != data_buffer.size()) return false; // Size mismatch
    return header.data_checksum == calculate_data_checksum(data_buffer.data(), data_buffer.size());
}

bool EventReplayEngine::read_next_event(EventHeader& out_header, std::vector<std::byte>& out_data_buffer) {
    if (!log_file_stream_.is_open() || log_file_stream_.eof() || !log_file_stream_.good()) {
        return false;
    }

    // Assuming binary format for replay engine
    log_file_stream_.read(reinterpret_cast<char*>(&out_header), sizeof(EventHeader));
    if (log_file_stream_.gcount() != sizeof(EventHeader)) {
        return false; // Could not read full header (EOF or error)
    }

    if (out_header.data_size_bytes > 0) {
        out_data_buffer.resize(out_header.data_size_bytes);
        log_file_stream_.read(reinterpret_cast<char*>(out_data_buffer.data()), out_header.data_size_bytes);
        if (log_file_stream_.gcount() != static_cast<std::streamsize>(out_header.data_size_bytes)) {
            out_data_buffer.clear(); // Read failed
            return false;
        }
    } else {
        out_data_buffer.clear();
    }

    if (!validate_event_checksum(out_header, out_data_buffer)) {
        std::cerr << "EventReplayEngine: Checksum mismatch for event seq=" << out_header.sequence_number << std::endl;
        // Decide: skip event or stop replay? For now, skip.
        return true; // Still successfully read, but data might be corrupt
    }
    return true;
}

void EventReplayEngine::replay_loop(uint64_t start_sequence_num) {
    is_replaying_ = true;
    is_paused_ = false;
    current_replay_sequence_number_ = 0; // Will be updated as we read

    EventHeader header;
    std::vector<std::byte> data_buffer;

    // Seek to start_sequence_num if needed (more complex, requires scanning)
    // For simplicity, this loop reads from current position. User should position file stream if seeking needed,
    // or we read and discard until start_sequence_num.
    bool found_start_seq = (start_sequence_num == 0);


    while (is_replaying_.load() && read_next_event(header, data_buffer)) {
        while (is_paused_.load() && is_replaying_.load()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
        if (!is_replaying_.load()) break;

        if (!found_start_seq) {
            if (header.sequence_number >= start_sequence_num) {
                found_start_seq = true;
            } else {
                current_replay_sequence_number_ = header.sequence_number; // Keep track even if skipping
                continue; // Skip events until start_sequence_num
            }
        }
        
        current_replay_sequence_number_ = header.sequence_number;

        if (replay_speed_factor_ > 0 && last_event_original_timestamp_ != TimePoint::min()) {
            TimePoint current_event_original_timestamp(std::chrono::nanoseconds(header.timestamp_ns));
            auto original_delta = current_event_original_timestamp - last_event_original_timestamp_;
            if (original_delta > std::chrono::nanoseconds::zero()) {
                 auto replay_delta_ns = static_cast<long long>(static_cast<double>(original_delta.count()) / replay_speed_factor_);
                 std::this_thread::sleep_for(std::chrono::nanoseconds(replay_delta_ns));
            }
        }
        last_event_original_timestamp_ = TimePoint(std::chrono::nanoseconds(header.timestamp_ns));
        
        if (event_callback_) {
            event_callback_(header, data_buffer);
        }

        if (replay_speed_factor_ == 0.0) { // Step-through mode
            is_paused_ = true; // Pause after each event
        }
    }
    is_replaying_ = false;
    is_paused_ = false;
     // std::cout << "EventReplayEngine: Replay loop finished." << std::endl;
}


void EventReplayEngine::start_replay(uint64_t start_sequence_number) {
    if (is_replaying_.load()) {
        // std::cout << "EventReplayEngine: Replay already in progress." << std::endl;
        return;
    }
    if (!log_file_stream_.is_open() || !log_file_stream_.good()){
        throw std::runtime_error("EventReplayEngine: Log file not open or in bad state for replay.");
    }
    
    // Reset EOF flags and position to start (or to where header check left it)
    log_file_stream_.clear(); 
    // If a binary header was read, current position is after it. Otherwise, it's at beg.
    // For now, assume replay always starts after the magic header if it was present.
    // Proper seeking to start_sequence_number is more complex.
    // This simple version starts from current file position effectively.

    stop_replay(); // Ensure any previous thread is joined
    
    is_replaying_ = true; // Set before starting thread
    is_paused_ = false;
    current_replay_sequence_number_ = 0; // Reset for new replay
    last_event_original_timestamp_ = TimePoint::min();

    // Start the replay loop in a new thread
    replay_thread_ = std::thread(&EventReplayEngine::replay_loop, this, start_sequence_number);
}

void EventReplayEngine::pause_replay() {
    is_paused_ = true;
}

void EventReplayEngine::resume_replay() {
    is_paused_ = false;
}

void EventReplayEngine::stop_replay() {
    is_replaying_ = false; // Signal loop to stop
    is_paused_ = false;   // Ensure it's not stuck in pause
    if (replay_thread_.joinable()) {
        replay_thread_.join();
    }
}

void EventReplayEngine::set_replay_speed(double speed_factor) {
    replay_speed_factor_ = std::max(0.0, speed_factor); // Allow 0 for step-through
}

bool EventReplayEngine::is_eof() const {
    // Note: const_cast is not ideal but ifstream::eof() is not const.
    // Better to check stream state like good() or fail() if possible.
    return log_file_stream_.eof();
}


} // namespace Alaris::Core