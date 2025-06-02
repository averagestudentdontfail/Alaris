// src/quantlib/ipc/shared_memory.cpp
#include "shared_ring_buffer.h"
#include "message_types.h"
#include "../core/time_type.h"
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#include <stdexcept>
#include <cstring>
#include <new>
#include <thread>
#include <algorithm>

namespace Alaris::IPC {

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::SharedRingBuffer(const char* name, bool create) 
    : shared_memory_region_(nullptr),
      header_(nullptr),
      buffer_(nullptr),
      is_owner_(create),
      shm_fd_(-1),
      shm_name_(name),
      last_access_time_(Core::Timing::Clock::now()),
      consecutive_empty_reads_(0),
      consecutive_full_writes_(0)
{
    static_assert(std::is_trivially_copyable_v<T>, 
                  "SharedRingBuffer type T must be trivially copyable for TTA shared memory usage");
    static_assert((Size > 0) && ((Size & (Size - 1)) == 0), 
                  "SharedRingBuffer Size must be a positive power of 2 for TTA efficiency");

    const size_t total_region_size = sizeof(Header) + (sizeof(T) * Size);

    if (create) {
        // Create new shared memory object with TTA-optimized flags
        shm_fd_ = shm_open(name, O_CREAT | O_RDWR | O_EXCL, 0660);
        if (shm_fd_ == -1) {
            if (errno == EEXIST) {
                // Already exists, try opening for TTA compatibility
                is_owner_ = false;
                shm_fd_ = shm_open(name, O_RDWR, 0660);
                if (shm_fd_ == -1) {
                    perror(("TTA SharedRingBuffer: Failed to open existing for " + std::string(name)).c_str());
                    throw std::runtime_error("TTA SharedRingBuffer: Failed to open existing shared memory: " + std::string(name));
                }
            } else {
                perror(("TTA SharedRingBuffer: shm_open O_CREAT failed for " + std::string(name)).c_str());
                throw std::runtime_error("TTA SharedRingBuffer: Failed to create shared memory: " + std::string(name));
            }
        } else {
            // Successfully created - set size for TTA deterministic access
            is_owner_ = true;
            if (ftruncate(shm_fd_, static_cast<off_t>(total_region_size)) == -1) {
                perror(("TTA SharedRingBuffer: ftruncate failed for " + std::string(name)).c_str());
                close(shm_fd_);
                shm_unlink(name);
                throw std::runtime_error("TTA SharedRingBuffer: Failed to set shared memory size for " + std::string(name));
            }
        }
    } else {
        is_owner_ = false;
        shm_fd_ = shm_open(name, O_RDWR, 0660);
        if (shm_fd_ == -1) {
            perror(("TTA SharedRingBuffer: Failed to open existing for " + std::string(name)).c_str());
            throw std::runtime_error("TTA SharedRingBuffer: Failed to open existing shared memory: " + std::string(name));
        }
    }
    
    // Map shared memory with TTA-optimized flags for deterministic access
    shared_memory_region_ = mmap(nullptr, total_region_size, 
                               PROT_READ | PROT_WRITE, 
                               MAP_SHARED, shm_fd_, 0);

    if (shared_memory_region_ == MAP_FAILED) {
        perror(("TTA SharedRingBuffer: mmap failed for " + std::string(name)).c_str());
        if (shm_fd_ != -1) close(shm_fd_);
        if (is_owner_ && shm_name_ != UNLINKED_SHM_NAME) {
            shm_unlink(name);
        }
        throw std::runtime_error("TTA SharedRingBuffer: Failed to map shared memory for " + std::string(name));
    }
    
    // TTA optimization: advise kernel about access patterns
    if (madvise(shared_memory_region_, total_region_size, MADV_WILLNEED) != 0) {
        // Non-fatal warning for TTA optimization
        perror("TTA SharedRingBuffer: madvise MADV_WILLNEED failed (non-fatal)");
    }
    
    // Sequential access pattern optimization for TTA predictability
    if (madvise(shared_memory_region_, total_region_size, MADV_SEQUENTIAL) != 0) {
        perror("TTA SharedRingBuffer: madvise MADV_SEQUENTIAL failed (non-fatal)");
    }
    
    // Assign pointers within the mapped region
    header_ = static_cast<Header*>(shared_memory_region_);
    buffer_ = reinterpret_cast<T*>(static_cast<std::byte*>(shared_memory_region_) + sizeof(Header));
    
    // Initialize header and buffer only if this process created the shared memory
    if (is_owner_) {
        // Use placement new for proper atomic initialization
        new (static_cast<void*>(header_)) Header();
        
        // Initialize buffer elements with placement new for TTA determinism
        for (size_t i = 0; i < Size; ++i) {
            new (static_cast<void*>(&buffer_[i])) T();
        }
        
        // Memory barrier to ensure initialization is complete before other processes access
        std::atomic_thread_fence(std::memory_order_release);
    } else {
        // Wait for initialization by owner (bounded wait for TTA)
        auto start_time = Core::Timing::Clock::now();
        constexpr auto MAX_INIT_WAIT = std::chrono::milliseconds(100);
        
        while (header_->write_index.load(std::memory_order_acquire) == UINT64_MAX) {
            if (Core::Timing::Clock::now() - start_time > MAX_INIT_WAIT) {
                throw std::runtime_error("TTA SharedRingBuffer: Timeout waiting for owner initialization");
            }
            std::this_thread::sleep_for(std::chrono::microseconds(10));
        }
    }
}

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::~SharedRingBuffer() {
    if (shared_memory_region_ && shared_memory_region_ != MAP_FAILED) {
        // TTA-safe cleanup: ensure all operations complete
        std::atomic_thread_fence(std::memory_order_seq_cst);
        
        munmap(shared_memory_region_, sizeof(Header) + (sizeof(T) * Size));
        shared_memory_region_ = nullptr;
    }
    
    if (shm_fd_ != -1) {
        close(shm_fd_);
        shm_fd_ = -1;
    }
    
    // Only the owner should unlink the shared memory object
    if (is_owner_ && !shm_name_.empty() && shm_name_ != UNLINKED_SHM_NAME) {
        shm_unlink(shm_name_.c_str());
        shm_name_ = UNLINKED_SHM_NAME;
    }
}

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::SharedRingBuffer(SharedRingBuffer&& other) noexcept
    : shared_memory_region_(other.shared_memory_region_),
      header_(other.header_),
      buffer_(other.buffer_),
      is_owner_(other.is_owner_),
      shm_fd_(other.shm_fd_),
      shm_name_(std::move(other.shm_name_)),
      last_access_time_(other.last_access_time_),
      consecutive_empty_reads_(other.consecutive_empty_reads_),
      consecutive_full_writes_(other.consecutive_full_writes_) {
      
    // Invalidate other to prevent double cleanup
    other.shared_memory_region_ = nullptr;
    other.header_ = nullptr;
    other.buffer_ = nullptr;
    other.is_owner_ = false;
    other.shm_fd_ = -1;
    other.shm_name_ = UNLINKED_SHM_NAME;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::try_write(const T& item) {
    if (!header_) [[unlikely]] return false;

    // TTA optimization: single atomic load for write index
    const uint64_t current_write_idx = header_->write_index.load(std::memory_order_relaxed);
    const uint64_t current_read_idx = header_->read_index.load(std::memory_order_acquire);
    
    // Check if buffer is full (bounded check)
    if (current_write_idx - current_read_idx >= Size) [[unlikely]] {
        consecutive_full_writes_++;
        update_metrics_on_contention();
        return false;
    }
    
    // Reset consecutive full writes on successful space check
    consecutive_full_writes_ = 0;
    
    // TTA-optimized memory copy with cache-line awareness
    const size_t slot_index = current_write_idx & MASK;
    std::memcpy(static_cast<void*>(&buffer_[slot_index]), 
                static_cast<const void*>(&item), 
                sizeof(T));
    
    // Memory barrier to ensure data is written before index update
    std::atomic_thread_fence(std::memory_order_release);
    
    // Update write index atomically
    header_->write_index.store(current_write_idx + 1, std::memory_order_release);
    
    // Update TTA metrics
    update_metrics_on_write();
    
    return true;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::try_read(T& item) {
    if (!header_) [[unlikely]] return false;

    // TTA optimization: single atomic load for read index
    const uint64_t current_read_idx = header_->read_index.load(std::memory_order_relaxed);
    const uint64_t current_write_idx = header_->write_index.load(std::memory_order_acquire);
    
    // Check if buffer is empty (bounded check)
    if (current_read_idx == current_write_idx) [[unlikely]] {
        consecutive_empty_reads_++;
        return false;
    }
    
    // Reset consecutive empty reads on successful data availability
    consecutive_empty_reads_ = 0;
    
    // TTA-optimized memory copy
    const size_t slot_index = current_read_idx & MASK;
    std::memcpy(static_cast<void*>(&item), 
                static_cast<const void*>(&buffer_[slot_index]), 
                sizeof(T));
    
    // Memory barrier to ensure data is read before index update
    std::atomic_thread_fence(std::memory_order_acquire);
    
    // Update read index atomically
    header_->read_index.store(current_read_idx + 1, std::memory_order_release);
    
    // Update TTA metrics
    update_metrics_on_read();
    
    return true;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::try_write_batch(const T* items, size_t count) {
    if (!header_ || count == 0) [[unlikely]] return 0;

    const uint64_t current_write_idx = header_->write_index.load(std::memory_order_relaxed);
    const uint64_t current_read_idx = header_->read_index.load(std::memory_order_acquire);
    
    const size_t available_slots = Size - (current_write_idx - current_read_idx);
    const size_t num_to_write = std::min(count, available_slots);
    
    if (num_to_write == 0) [[unlikely]] {
        consecutive_full_writes_++;
        update_metrics_on_contention();
        return 0;
    }

    consecutive_full_writes_ = 0;

    // TTA-optimized batch copy with cache-line awareness
    for (size_t i = 0; i < num_to_write; ++i) {
        const size_t slot_index = (current_write_idx + i) & MASK;
        std::memcpy(static_cast<void*>(&buffer_[slot_index]), 
                    static_cast<const void*>(&items[i]), 
                    sizeof(T));
    }
    
    std::atomic_thread_fence(std::memory_order_release);
    header_->write_index.store(current_write_idx + num_to_write, std::memory_order_release);
    
    // Update metrics for batch operation
    header_->total_writes.fetch_add(num_to_write, std::memory_order_relaxed);
    last_access_time_ = Core::Timing::Clock::now();
    
    return num_to_write;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::try_read_batch(T* items, size_t count) {
    if (!header_ || count == 0) [[unlikely]] return 0;

    const uint64_t current_read_idx = header_->read_index.load(std::memory_order_relaxed);
    const uint64_t current_write_idx = header_->write_index.load(std::memory_order_acquire);
    
    const size_t available_items = current_write_idx - current_read_idx;
    const size_t num_to_read = std::min(count, available_items);

    if (num_to_read == 0) [[unlikely]] {
        consecutive_empty_reads_++;
        return 0;
    }

    consecutive_empty_reads_ = 0;

    // TTA-optimized batch copy
    for (size_t i = 0; i < num_to_read; ++i) {
        const size_t slot_index = (current_read_idx + i) & MASK;
        std::memcpy(static_cast<void*>(&items[i]), 
                    static_cast<const void*>(&buffer_[slot_index]), 
                    sizeof(T));
    }
    
    std::atomic_thread_fence(std::memory_order_acquire);
    header_->read_index.store(current_read_idx + num_to_read, std::memory_order_release);
    
    // Update metrics for batch operation
    header_->total_reads.fetch_add(num_to_read, std::memory_order_relaxed);
    last_access_time_ = Core::Timing::Clock::now();
    
    return num_to_read;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::size() const {
    if (!header_) [[unlikely]] return 0;
    
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    return static_cast<size_t>(write_idx - read_idx);
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::empty() const {
    if (!header_) [[unlikely]] return true;
    
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    return write_idx == read_idx;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::full() const {
    if (!header_) [[unlikely]] return false;
    
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    return (write_idx - read_idx) >= Size;
}

template<typename T, size_t Size>
double SharedRingBuffer<T, Size>::utilization() const {
    if (Size == 0) [[unlikely]] return 0.0;
    return static_cast<double>(size()) / static_cast<double>(Size);
}

// TTA Performance monitoring implementation
template<typename T, size_t Size>
typename SharedRingBuffer<T, Size>::TTAMetrics 
SharedRingBuffer<T, Size>::get_tta_metrics() const {
    if (!header_) {
        return TTAMetrics{};
    }
    
    TTAMetrics metrics;
    metrics.total_writes = header_->total_writes.load(std::memory_order_relaxed);
    metrics.total_reads = header_->total_reads.load(std::memory_order_relaxed);
    metrics.contention_events = header_->contention_events.load(std::memory_order_relaxed);
    metrics.max_queue_depth = header_->max_queue_depth.load(std::memory_order_relaxed);
    metrics.consecutive_empty_reads = consecutive_empty_reads_;
    metrics.consecutive_full_writes = consecutive_full_writes_;
    
    auto current_time = Core::Timing::Clock::now();
    metrics.time_since_last_access = current_time - last_access_time_;
    
    // Calculate average queue depth
    const uint64_t total_operations = metrics.total_reads + metrics.total_writes;
    if (total_operations > 0) {
        const uint64_t current_size = size();
        metrics.average_queue_depth = static_cast<double>(current_size);
    } else {
        metrics.average_queue_depth = 0.0;
    }
    
    // Calculate contention rate
    if (total_operations > 0) {
        metrics.contention_rate = static_cast<double>(metrics.contention_events) / 
                                static_cast<double>(total_operations);
    } else {
        metrics.contention_rate = 0.0;
    }
    
    return metrics;
}

template<typename T, size_t Size>
void SharedRingBuffer<T, Size>::reset_tta_metrics() {
    if (!header_) return;
    
    header_->total_writes.store(0, std::memory_order_relaxed);
    header_->total_reads.store(0, std::memory_order_relaxed);
    header_->contention_events.store(0, std::memory_order_relaxed);
    header_->max_queue_depth.store(0, std::memory_order_relaxed);
    consecutive_empty_reads_ = 0;
    consecutive_full_writes_ = 0;
    last_access_time_ = Core::Timing::Clock::now();
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::is_tta_healthy() const {
    const auto metrics = get_tta_metrics();
    
    // TTA health criteria
    const bool low_contention = metrics.contention_rate < 0.05; // < 5% contention
    const bool reasonable_queue_depth = metrics.average_queue_depth < (Size * 0.8); // < 80% full
    const bool recent_activity = metrics.time_since_last_access < std::chrono::seconds(5);
    const bool no_starvation = metrics.consecutive_empty_reads < 1000 && 
                              metrics.consecutive_full_writes < 1000;
    
    return low_contention && reasonable_queue_depth && recent_activity && no_starvation;
}

// Private helper methods for TTA metrics
template<typename T, size_t Size>
void SharedRingBuffer<T, Size>::update_metrics_on_write() const {
    header_->total_writes.fetch_add(1, std::memory_order_relaxed);
    last_access_time_ = Core::Timing::Clock::now();
    
    // Update max queue depth
    const uint64_t current_size = size();
    uint64_t current_max = header_->max_queue_depth.load(std::memory_order_relaxed);
    while (current_size > current_max && 
           !header_->max_queue_depth.compare_exchange_weak(current_max, current_size, std::memory_order_relaxed)) {
        // Retry until successful or no longer max
    }
}

template<typename T, size_t Size>
void SharedRingBuffer<T, Size>::update_metrics_on_read() const {
    header_->total_reads.fetch_add(1, std::memory_order_relaxed);
    last_access_time_ = Core::Timing::Clock::now();
}

template<typename T, size_t Size>
void SharedRingBuffer<T, Size>::update_metrics_on_contention() const {
    header_->contention_events.fetch_add(1, std::memory_order_relaxed);
}

template<typename T, size_t Size>
void SharedRingBuffer<T, Size>::tta_backoff() const {
    // TTA-compliant minimal backoff
    std::this_thread::sleep_for(std::chrono::nanoseconds(CONTENTION_BACKOFF_NS));
}

// Explicit template instantiations for TTA message types
template class SharedRingBuffer<MarketDataMessage, 4096>;
template class SharedRingBuffer<TradingSignalMessage, 1024>;
template class SharedRingBuffer<ControlMessage, 256>;

} // namespace Alaris::IPC