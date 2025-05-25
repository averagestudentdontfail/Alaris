// src/quantlib/ipc/shared_ring_buffer.h
#pragma once

#include <atomic>
#include <cstdint>
#include <type_traits>
#include <string>

namespace Alaris::IPC {

template<typename T, size_t Size>
class SharedRingBuffer {
    static_assert(std::is_trivially_copyable_v<T>, "T must be trivially copyable");
    static_assert((Size & (Size - 1)) == 0, "Size must be power of 2");
    
private:
    struct alignas(64) Header {
        std::atomic<uint64_t> write_index{0};
        std::atomic<uint64_t> read_index{0};
        char padding[64 - sizeof(std::atomic<uint64_t>) * 2];
    };
    
    // Member variables ordered to match initialization order in constructor
    void* shared_memory_region_;  // Must be initialized first as it's used by other members
    Header* header_;              // Initialized second, depends on shared_memory_region_
    T* buffer_;                   // Initialized third, depends on header_
    bool is_owner_;               // Simple bool, no dependencies
    int shm_fd_;                  // File descriptor, no dependencies
    std::string shm_name_;        // String, no dependencies
    static constexpr const char* UNLINKED_SHM_NAME = "UNLINKED";
    
    static constexpr uint64_t MASK = Size - 1;
    
public:
    explicit SharedRingBuffer(const char* name, bool create = true);
    ~SharedRingBuffer();
    
    // Non-copyable
    SharedRingBuffer(const SharedRingBuffer&) = delete;
    SharedRingBuffer& operator=(const SharedRingBuffer&) = delete;
    
    // Movable
    SharedRingBuffer(SharedRingBuffer&& other) noexcept;
    SharedRingBuffer& operator=(SharedRingBuffer&& other) noexcept;
    
    // Non-blocking operations
    bool try_write(const T& item);
    bool try_read(T& item);
    
    // Batch operations for efficiency
    size_t try_write_batch(const T* items, size_t count);
    size_t try_read_batch(T* items, size_t count);
    
    // Status queries
    size_t size() const;
    bool empty() const;
    bool full() const;
    double utilization() const;
    
    // Performance monitoring
    uint64_t total_writes() const { return header_->write_index.load(std::memory_order_relaxed); }
    uint64_t total_reads() const { return header_->read_index.load(std::memory_order_relaxed); }
};

} // namespace Alaris::IPC