#include "event_log.h"
#include "time_type.h"
#include <stdexcept>
#include <iomanip>
#include <cstring>
#include <vector>
#include <iostream>
#include <sstream>
#include <algorithm>

namespace {
    uint32_t internal_calculate_checksum_eventlog(const void* data, size_t size, uint32_t initial_crc = 0xFFFFFFFF) {
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
}

namespace Alaris::Core {

// EventLogger Implementation
EventLogger::EventLogger(const std::string& filename, bool binary_mode)
    : current_sequence_number_(0),
      log_filename_(filename),
      use_binary_format_(binary_mode),
      total_events_logged_count_(0),
      total_bytes_written_count_(0) {
    
    std::ios_base::openmode open_flags = std::ios::out | std::ios::app;
    if (use_binary_format_) {
        open_flags |= std::ios::binary;
    }
    
    log_file_stream_.open(log_filename_, open_flags);
    if (!log_file_stream_.is_open()) {
        std::cerr << "CRITICAL: EventLogger failed to open log file: " << log_filename_ << std::endl;
        throw std::runtime_error("EventLogger: Failed to open log file: " + log_filename_);
    }

    log_file_stream_.seekp(0, std::ios::end);
    if (log_file_stream_.tellp() == 0) {
        if (use_binary_format_) {
            const char binary_header_magic[] = "ALARISLOG_V1B";
            log_file_stream_.write(binary_header_magic, sizeof(binary_header_magic) - 1);
            total_bytes_written_count_ += static_cast<uint64_t>(sizeof(binary_header_magic) - 1);
        } else {
            const char* text_header = "# Alaris Event Log V1.0 (Text Format)\n"
                                      "# Timestamp(ns),Sequence,EventType,DataSize,Checksum,Payload(Hex)\n";
            log_file_stream_ << text_header;
            total_bytes_written_count_ += static_cast<uint64_t>(std::strlen(text_header));
        }
        log_file_stream_.flush();
    }
}

EventLogger::~EventLogger() {
    if (log_file_stream_.is_open()) {
        flush_log();
        log_file_stream_.close();
    }
}

uint32_t EventLogger::calculate_data_checksum(const void* data, size_t size) const {
    return internal_calculate_checksum_eventlog(data, size);
}

void EventLogger::write_log_entry(EventType type, const void* data_payload, size_t payload_size) {
    EventHeader header;
    auto now = Timing::Clock::now();
    header.timestamp_ns = static_cast<uint64_t>(std::chrono::duration_cast<std::chrono::nanoseconds>(now.time_since_epoch()).count());
    header.sequence_number = current_sequence_number_++;
    header.event_type = type;
    header.data_size_bytes = static_cast<uint32_t>(payload_size);
    header.data_checksum = (payload_size > 0 && data_payload) ? calculate_data_checksum(data_payload, payload_size) : 0;

    size_t bytes_written_this_event = 0;
    std::string text_line_buffer;

    std::lock_guard<std::mutex> lock(log_mutex_);
    if (!log_file_stream_.is_open() || !log_file_stream_.good()) {
        std::cerr << "EventLogger: Log file '" << log_filename_ << "' is not open or in a bad state. Event (Type: " 
                  << static_cast<uint32_t>(type) << ", Seq: " << header.sequence_number << ") lost." << std::endl;
        return;
    }

    if (use_binary_format_) {
        log_file_stream_.write(reinterpret_cast<const char*>(&header), sizeof(EventHeader));
        if (payload_size > 0 && data_payload) {
            log_file_stream_.write(static_cast<const char*>(data_payload), static_cast<std::streamsize>(payload_size));
        }
        bytes_written_this_event = sizeof(EventHeader) + payload_size;
    } else {
        std::ostringstream oss;
        oss << header.timestamp_ns << ","
            << header.sequence_number << ","
            << static_cast<uint32_t>(header.event_type) << ","
            << header.data_size_bytes << ","
            << header.data_checksum << ",";
        
        if (payload_size > 0 && data_payload) {
            // For text data (like system status), write as plain text
            if (type == EventType::SYSTEM_STATUS_CHANGE || 
                type == EventType::ERROR_LOG || 
                type == EventType::WARNING_LOG || 
                type == EventType::INFO_LOG || 
                type == EventType::DEBUG_LOG) {
                // Write as quoted string for readability
                std::string text_payload(static_cast<const char*>(data_payload), payload_size);
                oss << "\"" << text_payload << "\"";
            } else {
                // For binary data, write as hex
                const unsigned char* char_data = static_cast<const unsigned char*>(data_payload);
                oss << std::hex << std::setfill('0');
                for (size_t i = 0; i < payload_size; ++i) {
                    oss << std::setw(2) << static_cast<int>(char_data[i]);
                }
            }
        }
        oss << "\n";
        text_line_buffer = oss.str();
        log_file_stream_ << text_line_buffer;
        bytes_written_this_event = text_line_buffer.length();
    }
    
    // CRITICAL: Always flush after writing to ensure data is written to disk
    log_file_stream_.flush();

    total_events_logged_count_++;
    total_bytes_written_count_ += bytes_written_this_event;
}

// Specific log methods
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

void EventLogger::log_error(const std::string& error_msg) {
    write_log_entry(EventType::ERROR_LOG, error_msg.c_str(), error_msg.length());
}

void EventLogger::log_warning(const std::string& warning_msg) {
    write_log_entry(EventType::WARNING_LOG, warning_msg.c_str(), warning_msg.length());
}

void EventLogger::log_info(const std::string& info_msg) {
    write_log_entry(EventType::INFO_LOG, info_msg.c_str(), info_msg.length());
}

void EventLogger::log_debug(const std::string& debug_msg) {
    write_log_entry(EventType::DEBUG_LOG, debug_msg.c_str(), debug_msg.length());
}

void EventLogger::log_performance_metric(const std::string& metric_name, double metric_value) {
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
    log_filename_ = new_filename;
    std::ios_base::openmode open_flags = std::ios::out | std::ios::app;
    if (use_binary_format_) {
        open_flags |= std::ios::binary;
    }
    log_file_stream_.open(log_filename_, open_flags);
    if (!log_file_stream_.is_open()) {
        std::cerr << "CRITICAL: EventLogger failed to open new log file for rotation: " << log_filename_ << std::endl;
        throw std::runtime_error("EventLogger: Failed to open new log file for rotation: " + log_filename_);
    }
    log_file_stream_.seekp(0, std::ios::end);
    if (log_file_stream_.tellp() == 0) {
         if (use_binary_format_) {
            const char binary_header_magic[] = "ALARISLOG_V1B";
            log_file_stream_.write(binary_header_magic, sizeof(binary_header_magic)-1);
        } else {
            const char* text_header = "# Alaris Event Log V1.0 (Text Format)\n"
                                      "# Timestamp(ns),Sequence,EventType,DataSize,Checksum,Payload(Hex)\n";
            log_file_stream_ << text_header;
        }
        log_file_stream_.flush();
    }
}

bool EventLogger::is_healthy() const {
    std::lock_guard<std::mutex> lock(log_mutex_);
    return log_file_stream_.is_open() && log_file_stream_.good();
}

// EventReplayEngine Implementation
EventReplayEngine::EventReplayEngine(const std::string& log_filename, EventReplayCallback callback)
    : event_callback_(std::move(callback)),
      is_replaying_(false),
      is_paused_(false),
      replay_speed_factor_(1.0),
      current_replay_sequence_number_(0),
      last_event_original_timestamp_(TimePoint::min()),
      replay_session_start_host_time_(TimePoint::min()),
      replay_session_first_event_original_offset_ns_(Duration::zero()) {

    std::ios_base::openmode open_flags = std::ios::in | std::ios::binary;
    
    log_file_stream_.open(log_filename, open_flags);
    if (!log_file_stream_.is_open()) {
        std::cerr << "CRITICAL: EventReplayEngine failed to open log file: " << log_filename << std::endl;
        throw std::runtime_error("EventReplayEngine: Failed to open log file: " + log_filename);
    }

    char binary_header_check[13];
    std::streamsize header_len_to_check = static_cast<std::streamsize>(sizeof(binary_header_check));
    log_file_stream_.read(binary_header_check, header_len_to_check);

    if (!log_file_stream_ || log_file_stream_.gcount() != header_len_to_check || 
        std::string(binary_header_check, static_cast<size_t>(header_len_to_check)) != "ALARISLOG_V1B") {
        std::cout << "EventReplayEngine: Warning - Binary log header 'ALARISLOG_V1B' not found or mismatched in '" << log_filename 
                  << "'. Assuming headerless or text log (text replay not fully supported)." << std::endl;
        log_file_stream_.clear();
        log_file_stream_.seekg(0, std::ios::beg);
    }
}

EventReplayEngine::~EventReplayEngine() {
    stop_replay();
    if (log_file_stream_.is_open()) {
        log_file_stream_.close();
    }
}

uint32_t EventReplayEngine::calculate_data_checksum(const void* data, size_t size) const {
    return internal_calculate_checksum_eventlog(data, size);
}

bool EventReplayEngine::validate_event_checksum(const EventHeader& header, const std::vector<std::byte>& data_buffer) const {
    if (header.data_size_bytes == 0 && data_buffer.empty()) {
        return header.data_checksum == calculate_data_checksum(nullptr, 0);
    }
    if (header.data_size_bytes != data_buffer.size()) {
         std::cerr << "EventReplayEngine: Checksum validation size mismatch. Header: " << header.data_size_bytes 
                   << ", buffer: " << data_buffer.size() << std::endl;
        return false;
    }
    return header.data_checksum == calculate_data_checksum(data_buffer.data(), data_buffer.size());
}

bool EventReplayEngine::read_next_event(EventHeader& out_header, std::vector<std::byte>& out_data_buffer) {
    out_data_buffer.clear();

    if (!log_file_stream_.is_open() || !log_file_stream_.good() || log_file_stream_.eof()) {
        return false;
    }

    log_file_stream_.read(reinterpret_cast<char*>(&out_header), sizeof(EventHeader));
    if (log_file_stream_.gcount() != sizeof(EventHeader)) {
        return false;
    }

    if (out_header.data_size_bytes > 0) {
        const size_t MAX_EVENT_DATA_SIZE = 16 * 1024 * 1024;
        if (out_header.data_size_bytes > MAX_EVENT_DATA_SIZE) {
            std::cerr << "EventReplayEngine: Error - Event data size " << out_header.data_size_bytes 
                      << " exceeds sanity limit for seq=" << out_header.sequence_number << ". Log may be corrupt." << std::endl;
            return false;
        }
        try {
            out_data_buffer.resize(out_header.data_size_bytes);
        } catch (const std::bad_alloc& e) {
            std::cerr << "EventReplayEngine: Error - Failed to resize data buffer to " << out_header.data_size_bytes 
                      << " for seq=" << out_header.sequence_number << ". Exception: " << e.what() << std::endl;
            return false;
        }
        log_file_stream_.read(reinterpret_cast<char*>(out_data_buffer.data()), static_cast<std::streamsize>(out_header.data_size_bytes));
        if (log_file_stream_.gcount() != static_cast<std::streamsize>(out_header.data_size_bytes)) {
            std::cerr << "EventReplayEngine: Error - Failed to read full payload for seq=" << out_header.sequence_number 
                      << ". Expected " << out_header.data_size_bytes << ", got " << log_file_stream_.gcount() << std::endl;
            return false;
        }
    }

    if (!validate_event_checksum(out_header, out_data_buffer)) {
        std::cerr << "EventReplayEngine: Warning - Checksum mismatch for event seq=" << out_header.sequence_number 
                  << ". Header checksum: " << out_header.data_checksum 
                  << ", Calculated: " << calculate_data_checksum(out_data_buffer.data(), out_data_buffer.size()) << std::endl;
    }
    return true;
}

void EventReplayEngine::replay_loop(uint64_t start_sequence_num) {
    EventHeader header;
    std::vector<std::byte> data_buffer;
    bool found_start_seq = (start_sequence_num == 0);
    bool first_event_after_skip = true;
    
    replay_session_start_host_time_ = Timing::Clock::now();

    while (is_replaying_.load(std::memory_order_acquire)) {
        while (is_paused_.load(std::memory_order_acquire) && is_replaying_.load(std::memory_order_acquire)) {
            std::this_thread::sleep_for(std::chrono::milliseconds(50));
        }
        if (!is_replaying_.load(std::memory_order_acquire)) break;

        if (!read_next_event(header, data_buffer)) {
            break;
        }
        
        current_replay_sequence_number_ = header.sequence_number;

        if (!found_start_seq) {
            if (header.sequence_number >= start_sequence_num) {
                found_start_seq = true;
                first_event_after_skip = true;
                replay_session_start_host_time_ = Timing::Clock::now();
            } else {
                continue;
            }
        }
        
        TimePoint current_event_original_ts(std::chrono::nanoseconds(header.timestamp_ns));

        if (first_event_after_skip) {
            replay_session_first_event_original_offset_ns_ = std::chrono::nanoseconds(header.timestamp_ns);
            last_event_original_timestamp_ = current_event_original_ts;
            first_event_after_skip = false;
        }
        
        if (replay_speed_factor_ > 0.0) {
            Duration original_elapsed_current_segment_ns = current_event_original_ts - TimePoint(replay_session_first_event_original_offset_ns_);
            Duration desired_host_time_for_event_ns(
                static_cast<long long>(static_cast<double>(original_elapsed_current_segment_ns.count()) / replay_speed_factor_)
            );
            
            TimePoint target_dispatch_host_time = replay_session_start_host_time_ + desired_host_time_for_event_ns;
            TimePoint current_host_time = Timing::Clock::now();

            if (target_dispatch_host_time > current_host_time) {
                std::this_thread::sleep_until(target_dispatch_host_time);
            }
        }
        last_event_original_timestamp_ = current_event_original_ts;

        if (event_callback_) {
            event_callback_(header, data_buffer);
        }

        if (replay_speed_factor_ == 0.0) {
            is_paused_ = true;
        }
    }
    is_replaying_ = false;
    is_paused_ = false;
}

void EventReplayEngine::start_replay(uint64_t start_sequence_number) {
    if (is_replaying_.load()) {
        std::cout << "EventReplayEngine: Replay is already in progress. Stop it first." << std::endl;
        return;
    }
    if (!log_file_stream_.is_open() || !log_file_stream_.good()){
        std::cerr << "CRITICAL: EventReplayEngine log file not open or in bad state for replay." << std::endl;
        throw std::runtime_error("EventReplayEngine: Log file not open or in bad state for replay.");
    }
    
    stop_replay();

    log_file_stream_.clear();
    log_file_stream_.seekg(0, std::ios::beg);
    
    char binary_header_check[13];
    std::streamsize header_len_to_check = static_cast<std::streamsize>(sizeof(binary_header_check));
    log_file_stream_.read(binary_header_check, header_len_to_check);
    if (!log_file_stream_ || log_file_stream_.gcount() != header_len_to_check || 
        std::string(binary_header_check, static_cast<size_t>(header_len_to_check)) != "ALARISLOG_V1B") {
        log_file_stream_.clear();
        log_file_stream_.seekg(0, std::ios::beg);
    }

    is_replaying_ = true;
    is_paused_ = false;
    last_event_original_timestamp_ = TimePoint(Duration::min());
    replay_session_start_host_time_ = TimePoint(Duration::min());
    replay_session_first_event_original_offset_ns_ = Duration::zero();

    replay_thread_ = std::thread(&EventReplayEngine::replay_loop, this, start_sequence_number);
}

void EventReplayEngine::pause_replay() {
    is_paused_ = true;
}

void EventReplayEngine::resume_replay() {
    is_paused_ = false;
}

void EventReplayEngine::stop_replay() {
    if (is_replaying_.exchange(false, std::memory_order_acq_rel)) {
        is_paused_ = false;
        if (replay_thread_.joinable()) {
            replay_thread_.join();
        }
    } else if (replay_thread_.joinable()){
         replay_thread_.join();
    }
}

void EventReplayEngine::set_replay_speed(double speed_factor) {
    replay_speed_factor_ = std::max(0.0, speed_factor);
}

bool EventReplayEngine::is_eof() const {
    return log_file_stream_.eof();
}

} // namespace Alaris::Core