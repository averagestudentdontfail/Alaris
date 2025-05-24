// src/quantlib/ipc/shared_ring_buffer.h
#pragma once

#include <atomic>
#include <cstdint>
#include <type_traits>

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
    
    Header* header_;
    T* buffer_;
    void* shared_memory_;
    bool is_owner_;
    
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