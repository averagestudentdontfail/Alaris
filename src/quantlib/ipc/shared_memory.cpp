// src/quantlib/ipc/shared_memory.cpp
#include "shared_ring_buffer.h"
#include "message_types.h"
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#include <stdexcept>
#include <cstring>

namespace Alaris::IPC {

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::SharedRingBuffer(const char* name, bool create) 
    : shared_memory_(nullptr), is_owner_(create) {
    
    const size_t total_size = sizeof(Header) + sizeof(T) * Size;
    
    int fd;
    if (create) {
        // Create new shared memory
        fd = shm_open(name, O_CREAT | O_RDWR, 0666);
        if (fd == -1) {
            throw std::runtime_error("Failed to create shared memory");
        }
        
        if (ftruncate(fd, total_size) == -1) {
            close(fd);
            shm_unlink(name);
            throw std::runtime_error("Failed to set shared memory size");
        }
    } else {
        // Open existing shared memory
        fd = shm_open(name, O_RDWR, 0666);
        if (fd == -1) {
            throw std::runtime_error("Failed to open shared memory");
        }
    }
    
    // Map memory
    shared_memory_ = mmap(nullptr, total_size, PROT_READ | PROT_WRITE, 
                         MAP_SHARED, fd, 0);
    close(fd);
    
    if (shared_memory_ == MAP_FAILED) {
        if (create) shm_unlink(name);
        throw std::runtime_error("Failed to map shared memory");
    }
    
    // Initialize pointers
    header_ = static_cast<Header*>(shared_memory_);
    buffer_ = reinterpret_cast<T*>(static_cast<char*>(shared_memory_) + sizeof(Header));
    
    // Initialize header if creating
    if (create) {
        new (header_) Header();
        std::memset(buffer_, 0, sizeof(T) * Size);
    }
}

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::~SharedRingBuffer() {
    if (shared_memory_ && shared_memory_ != MAP_FAILED) {
        munmap(shared_memory_, sizeof(Header) + sizeof(T) * Size);
    }
}

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::SharedRingBuffer(SharedRingBuffer&& other) noexcept
    : header_(other.header_), buffer_(other.buffer_), 
      shared_memory_(other.shared_memory_), is_owner_(other.is_owner_) {
    other.header_ = nullptr;
    other.buffer_ = nullptr;
    other.shared_memory_ = nullptr;
    other.is_owner_ = false;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::try_write(const T& item) {
    const uint64_t write_idx = header_->write_index.load(std::memory_order_relaxed);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    
    // Check if buffer is full
    if (write_idx - read_idx >= Size) {
        return false;
    }
    
    // Write item
    buffer_[write_idx & MASK] = item;
    
    // Update write index with release semantics
    header_->write_index.store(write_idx + 1, std::memory_order_release);
    
    return true;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::try_read(T& item) {
    const uint64_t read_idx = header_->read_index.load(std::memory_order_relaxed);
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    
    // Check if buffer is empty
    if (read_idx == write_idx) {
        return false;
    }
    
    // Read item
    item = buffer_[read_idx & MASK];
    
    // Update read index with release semantics
    header_->read_index.store(read_idx + 1, std::memory_order_release);
    
    return true;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::try_write_batch(const T* items, size_t count) {
    const uint64_t write_idx = header_->write_index.load(std::memory_order_relaxed);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    
    const size_t available = Size - (write_idx - read_idx);
    const size_t to_write = std::min(count, available);
    
    for (size_t i = 0; i < to_write; ++i) {
        buffer_[(write_idx + i) & MASK] = items[i];
    }
    
    header_->write_index.store(write_idx + to_write, std::memory_order_release);
    
    return to_write;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::try_read_batch(T* items, size_t count) {
    const uint64_t read_idx = header_->read_index.load(std::memory_order_relaxed);
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    
    const size_t available = write_idx - read_idx;
    const size_t to_read = std::min(count, available);
    
    for (size_t i = 0; i < to_read; ++i) {
        items[i] = buffer_[(read_idx + i) & MASK];
    }
    
    header_->read_index.store(read_idx + to_read, std::memory_order_release);
    
    return to_read;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::size() const {
    const uint64_t write_idx = header_->write_index.load(std::memory_order_relaxed);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_relaxed);
    return write_idx - read_idx;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::empty() const {
    return size() == 0;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::full() const {
    return size() >= Size;
}

template<typename T, size_t Size>
double SharedRingBuffer<T, Size>::utilization() const {
    return static_cast<double>(size()) / static_cast<double>(Size);
}

// Explicit template instantiations
template class SharedRingBuffer<MarketDataMessage, 4096>;
template class SharedRingBuffer<TradingSignalMessage, 1024>;
template class SharedRingBuffer<ControlMessage, 256>;

} // namespace Alaris::IPC
