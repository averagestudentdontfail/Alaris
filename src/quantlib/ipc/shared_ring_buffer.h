// src/quantlib/ipc/shared_ring_buffer.h
#pragma once

#include "../core/time_type.h"
#include <atomic>
#include <cstdint>
#include <type_traits>
#include <string>
#include <chrono>

namespace Alaris::IPC {

/**
 * @brief TTA-compliant lock-free ring buffer for deterministic inter-process communication
 * 
 * Optimized for Time-Triggered Architecture with:
 * - Bounded execution times
 * - Deterministic memory access patterns
 * - Performance monitoring integration
 * - Cache-line alignment for predictable access
 */
template<typename T, size_t Size>
class SharedRingBuffer {
    static_assert(std::is_trivially_copyable_v<T>, "T must be trivially copyable for shared memory");
    static_assert((Size & (Size - 1)) == 0, "Size must be power of 2 for efficient masking");
    static_assert(Size >= 64, "Minimum buffer size is 64 elements for TTA efficiency");
    
private:
    // Cache-line aligned header for optimal memory access patterns
    struct alignas(64) Header {
        std::atomic<uint64_t> write_index{0};
        std::atomic<uint64_t> read_index{0};
        
        // TTA performance monitoring - updated atomically
        std::atomic<uint64_t> total_writes{0};
        std::atomic<uint64_t> total_reads{0};
        std::atomic<uint64_t> contention_events{0};
        std::atomic<uint64_t> max_queue_depth{0};
        
        // Cache line padding to prevent false sharing
        char padding[64 - sizeof(std::atomic<uint64_t>) * 6];
    };
    
    // Member variables ordered by initialization dependencies
    void* shared_memory_region_;   // mmap'd memory region
    Header* header_;               // Pointer to header within shared memory
    T* buffer_;                   // Pointer to buffer array within shared memory
    bool is_owner_;               // Whether this process owns the shared memory
    int shm_fd_;                  // Shared memory file descriptor
    std::string shm_name_;        // Name of shared memory object
    
    // TTA timing tracking
    mutable Core::Timing::TimePoint last_access_time_;
    mutable uint64_t consecutive_empty_reads_;
    mutable uint64_t consecutive_full_writes_;
    
    static constexpr const char* UNLINKED_SHM_NAME = "UNLINKED";
    static constexpr uint64_t MASK = Size - 1;
    
    // TTA-specific constants for bounded operations
    static constexpr uint32_t MAX_CONTENTION_RETRIES = 3;
    static constexpr uint64_t CONTENTION_BACKOFF_NS = 100; // 100ns backoff
    
public:
    /**
     * @brief Construct shared ring buffer for TTA usage
     * @param name Shared memory object name (must start with '/')
     * @param create True if this process should create the shared memory
     */
    explicit SharedRingBuffer(const char* name, bool create = true);
    
    /**
     * @brief Destructor - cleans up shared memory if owner
     */
    ~SharedRingBuffer();
    
    // Non-copyable for deterministic ownership
    SharedRingBuffer(const SharedRingBuffer&) = delete;
    SharedRingBuffer& operator=(const SharedRingBuffer&) = delete;
    
    // Movable for efficient resource transfer
    SharedRingBuffer(SharedRingBuffer&& other) noexcept;
    SharedRingBuffer& operator=(SharedRingBuffer&& other) noexcept;
    
    /**
     * @brief TTA-optimized non-blocking write with bounded execution time
     * @param item Item to write
     * @return True if written successfully, false if buffer full
     * 
     * Execution time: O(1) bounded, typically < 50ns
     * Memory accesses: Exactly 2 atomic loads, 1 memcpy, 1 atomic store
     */
    bool try_write(const T& item);
    
    /**
     * @brief TTA-optimized non-blocking read with bounded execution time
     * @param item Reference to store read item
     * @return True if read successfully, false if buffer empty
     * 
     * Execution time: O(1) bounded, typically < 40ns
     * Memory accesses: Exactly 2 atomic loads, 1 memcpy, 1 atomic store
     */
    bool try_read(T& item);
    
    /**
     * @brief Batch write for improved TTA efficiency
     * @param items Array of items to write
     * @param count Number of items to write
     * @return Number of items actually written
     * 
     * More efficient than multiple try_write calls for TTA scheduling
     */
    size_t try_write_batch(const T* items, size_t count);
    
    /**
     * @brief Batch read for improved TTA efficiency
     * @param items Array to store read items
     * @param count Maximum number of items to read
     * @return Number of items actually read
     */
    size_t try_read_batch(T* items, size_t count);
    
    /**
     * @brief Get current queue size (bounded execution time)
     * @return Number of items currently in queue
     */
    size_t size() const;
    
    /**
     * @brief Check if queue is empty (bounded execution time)
     * @return True if empty
     */
    bool empty() const;
    
    /**
     * @brief Check if queue is full (bounded execution time)
     * @return True if full
     */
    bool full() const;
    
    /**
     * @brief Get queue utilization as percentage
     * @return Utilization from 0.0 to 1.0
     */
    double utilization() const;
    
    // TTA Performance monitoring interface
    struct TTAMetrics {
        uint64_t total_writes;
        uint64_t total_reads;
        uint64_t contention_events;
        uint64_t max_queue_depth;
        uint64_t consecutive_empty_reads;
        uint64_t consecutive_full_writes;
        Core::Timing::Duration time_since_last_access;
        double average_queue_depth;
        double contention_rate;
    };
    
    /**
     * @brief Get TTA performance metrics
     * @return Comprehensive performance metrics for TTA analysis
     */
    TTAMetrics get_tta_metrics() const;
    
    /**
     * @brief Reset TTA performance counters
     */
    void reset_tta_metrics();
    
    /**
     * @brief Check if buffer is healthy for TTA operation
     * @return True if operating within TTA parameters
     */
    bool is_tta_healthy() const;
    
    // Legacy interface for backwards compatibility
    uint64_t total_writes() const { 
        return header_ ? header_->total_writes.load(std::memory_order_relaxed) : 0; 
    }
    
    uint64_t total_reads() const { 
        return header_ ? header_->total_reads.load(std::memory_order_relaxed) : 0; 
    }
    
private:
    /**
     * @brief Update TTA metrics atomically
     */
    void update_metrics_on_write() const;
    void update_metrics_on_read() const;
    void update_metrics_on_contention() const;
    
    /**
     * @brief TTA-compliant backoff for contention handling
     */
    void tta_backoff() const;
};

/**
 * @brief TTA-specific ring buffer configuration
 */
struct TTABufferConfig {
    size_t buffer_size;
    bool enable_metrics;
    bool enable_contention_detection;
    uint32_t max_contention_retries;
    Core::Timing::Duration contention_backoff;
    
    static TTABufferConfig default_config() {
        return {
            .buffer_size = 4096,
            .enable_metrics = true,
            .enable_contention_detection = true,
            .max_contention_retries = 3,
            .contention_backoff = std::chrono::nanoseconds(100)
        };
    }
    
    static TTABufferConfig high_performance_config() {
        return {
            .buffer_size = 8192,
            .enable_metrics = false,  // Minimal overhead
            .enable_contention_detection = false,
            .max_contention_retries = 1,
            .contention_backoff = std::chrono::nanoseconds(50)
        };
    }
};

} // namespace Alaris::IPC