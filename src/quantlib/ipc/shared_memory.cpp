// src/quantlib/ipc/shared_memory.cpp
#include "shared_ring_buffer.h" // Contains template definition for SharedRingBuffer
#include "message_types.h"      // For MarketDataMessage, etc.
#include <sys/mman.h>
#include <sys/stat.h>
#include <fcntl.h>
#include <unistd.h>
#include <stdexcept>
#include <cstring> // For std::memcpy in read/write, not for initializing objects
#include <new>     // For placement new

namespace Alaris::IPC {

// Template implementation for SharedRingBuffer (should be in the header or .tpp if not specialized)
// Assuming it's here because it's instantiated at the end of this file.

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::SharedRingBuffer(const char* name, bool create) 
    : shared_memory_region_(nullptr), // Changed member name from shared_memory_ for clarity
      header_(nullptr),
      buffer_(nullptr),
      is_owner_(create) {
    
    static_assert(std::is_trivially_copyable_v<T>, "SharedRingBuffer type T must be trivially copyable for safe shared memory usage.");
    static_assert((Size > 0) && ((Size & (Size - 1)) == 0), "SharedRingBuffer Size must be a positive power of 2.");

    const size_t total_region_size = sizeof(Header) + (sizeof(T) * Size);
    shm_fd_ = -1; // Initialize shm_fd_

    if (create) {
        // Create new shared memory object
        shm_fd_ = shm_open(name, O_CREAT | O_RDWR | O_EXCL, 0660); // Use O_EXCL to ensure creation
        if (shm_fd_ == -1) {
            if (errno == EEXIST) { // Already exists, try opening
                is_owner_ = false; // Not the owner if it already existed
                shm_fd_ = shm_open(name, O_RDWR, 0660);
                if (shm_fd_ == -1) {
                    perror(("SharedRingBuffer: shm_open failed to open existing EEXIST for " + std::string(name)).c_str());
                    throw std::runtime_error("Failed to open existing shared memory: " + std::string(name));
                }
                // If we open existing, we don't ftruncate or initialize header/buffer with memset
            } else {
                perror(("SharedRingBuffer: shm_open O_CREAT failed for " + std::string(name)).c_str());
                throw std::runtime_error("Failed to create shared memory: " + std::string(name));
            }
        } else { // Successfully created
            is_owner_ = true; // Confirmed owner
            if (ftruncate(shm_fd_, static_cast<off_t>(total_region_size)) == -1) {
                perror(("SharedRingBuffer: ftruncate failed for " + std::string(name)).c_str());
                close(shm_fd_);
                shm_unlink(name); // Clean up
                throw std::runtime_error("Failed to set shared memory size for " + std::string(name));
            }
        }
    } else { // Open existing shared memory object (consumer case)
        is_owner_ = false;
        shm_fd_ = shm_open(name, O_RDWR, 0660);
        if (shm_fd_ == -1) {
            perror(("SharedRingBuffer: shm_open failed to open existing for " + std::string(name)).c_str());
            throw std::runtime_error("Failed to open existing shared memory: " + std::string(name));
        }
    }
    
    // Map shared memory object to process address space
    shared_memory_region_ = mmap(nullptr, total_region_size, PROT_READ | PROT_WRITE, 
                               MAP_SHARED, shm_fd_, 0);
    // close(shm_fd_); // fd can be closed after mmap, but keep it if needed for fstat/info for existing shm
                   // It's safer to close it to avoid fd exhaustion.

    if (shared_memory_region_ == MAP_FAILED) {
        perror(("SharedRingBuffer: mmap failed for " + std::string(name)).c_str());
        if (shm_fd_ != -1) close(shm_fd_);
        if (is_owner_ && shm_name_ != UNLINKED_SHM_NAME) { // Store name for unlink
             shm_unlink(name); // Clean up if owner and mapping failed
        }
        throw std::runtime_error("Failed to map shared memory for " + std::string(name));
    }
    
    // Assign pointers to header and buffer region
    header_ = static_cast<Header*>(shared_memory_region_);
    buffer_ = reinterpret_cast<T*>(static_cast<std::byte*>(shared_memory_region_) + sizeof(Header));
    shm_name_ = name; // Store name for unlink in destructor if owner
    
    // Initialize header and buffer memory only if this process created the shared memory segment
    if (is_owner_) { // Check if *this specific call* was the creator via O_EXCL success
        new (static_cast<void*>(header_)) Header(); // Placement new for the header (initializes atomics)
        
        // Initialize the buffer objects using placement new default construction
        // This runs the constructor for each object T in the buffer.
        for (size_t i = 0; i < Size; ++i) {
            new (static_cast<void*>(&buffer_[i])) T(); 
        }
    }
}

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::~SharedRingBuffer() {
    if (shared_memory_region_ && shared_memory_region_ != MAP_FAILED) {
        munmap(shared_memory_region_, sizeof(Header) + (sizeof(T) * Size));
        shared_memory_region_ = nullptr; // Mark as unmapped
    }
    if (shm_fd_ != -1) { // Close file descriptor if it's still open
        close(shm_fd_);
        shm_fd_ = -1;
    }
    // Only the creator/owner should unlink the shared memory object
    if (is_owner_ && !shm_name_.empty() && shm_name_ != UNLINKED_SHM_NAME) {
        shm_unlink(shm_name_.c_str());
        // std::cout << "SharedRingBuffer: Unlinked shared memory '" << shm_name_ << "'" << std::endl;
        shm_name_ = UNLINKED_SHM_NAME; // Mark as unlinked
    }
}

template<typename T, size_t Size>
SharedRingBuffer<T, Size>::SharedRingBuffer(SharedRingBuffer&& other) noexcept
    : shared_memory_region_(other.shared_memory_region_),
      header_(other.header_),
      buffer_(other.buffer_),
      is_owner_(other.is_owner_),
      shm_fd_(other.shm_fd_),
      shm_name_(std::move(other.shm_name_)) { // Move the name
      
    // Invalidate other to prevent double free/unlink
    other.shared_memory_region_ = nullptr;
    other.header_ = nullptr;
    other.buffer_ = nullptr;
    other.is_owner_ = false; // Ownership is transferred
    other.shm_fd_ = -1;
    other.shm_name_ = UNLINKED_SHM_NAME; // Mark other as not responsible for unlinking
}

// operator= can be similarly implemented if needed, or keep as deleted if move ctor is enough

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::try_write(const T& item) {
    if (!header_) return false; // Not initialized

    // Use relaxed memory order for initial read of write_idx, as it's mainly an optimistic check.
    uint64_t current_write_idx = header_->write_index.load(std::memory_order_relaxed);
    // Use acquire for read_idx to ensure visibility of previous writes by consumers (though less critical for producer here).
    uint64_t current_read_idx = header_->read_index.load(std::memory_order_acquire);
    
    if (current_write_idx - current_read_idx >= Size) {
        return false; // Buffer is full
    }
    
    // buffer_ is T*, so buffer_[index] is correct.
    // Use memcpy for trivially_copyable types to ensure byte-for-byte copy into shared memory.
    // Placement new with copy constructor could also be an option: new (&buffer_[current_write_idx & MASK]) T(item);
    // But for IPC with trivially copyable types, direct copy is common.
    std::memcpy(static_cast<void*>(&buffer_[current_write_idx & MASK]), static_cast<const void*>(&item), sizeof(T));
    
    // Use release semantics for store to ensure that the write to the buffer item
    // is visible before the write_index is updated.
    header_->write_index.store(current_write_idx + 1, std::memory_order_release);
    
    return true;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::try_read(T& item) {
    if (!header_) return false; // Not initialized

    // Use relaxed for read_idx initially.
    uint64_t current_read_idx = header_->read_index.load(std::memory_order_relaxed);
    // Use acquire for write_idx to ensure visibility of writes by producers.
    uint64_t current_write_idx = header_->write_index.load(std::memory_order_acquire);
    
    if (current_read_idx == current_write_idx) {
        return false; // Buffer is empty
    }
    
    std::memcpy(static_cast<void*>(&item), static_cast<const void*>(&buffer_[current_read_idx & MASK]), sizeof(T));
    
    // Use release semantics for store to make the slot available implicitly
    // and ensure other consumers see the updated read_index.
    header_->read_index.store(current_read_idx + 1, std::memory_order_release);
    
    return true;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::try_write_batch(const T* items, size_t count) {
    if (!header_ || count == 0) return 0;

    uint64_t current_write_idx = header_->write_index.load(std::memory_order_relaxed);
    uint64_t current_read_idx = header_->read_index.load(std::memory_order_acquire);
    
    const size_t available_slots = Size - (current_write_idx - current_read_idx);
    const size_t num_to_write = std::min(count, available_slots);
    
    if (num_to_write == 0) return 0;

    for (size_t i = 0; i < num_to_write; ++i) {
        std::memcpy(static_cast<void*>(&buffer_[(current_write_idx + i) & MASK]), static_cast<const void*>(&items[i]), sizeof(T));
    }
    
    header_->write_index.store(current_write_idx + num_to_write, std::memory_order_release);
    return num_to_write;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::try_read_batch(T* items, size_t count) {
    if (!header_ || count == 0) return 0;

    uint64_t current_read_idx = header_->read_index.load(std::memory_order_relaxed);
    uint64_t current_write_idx = header_->write_index.load(std::memory_order_acquire);
    
    const size_t available_items = current_write_idx - current_read_idx;
    const size_t num_to_read = std::min(count, available_items);

    if (num_to_read == 0) return 0;

    for (size_t i = 0; i < num_to_read; ++i) {
        std::memcpy(static_cast<void*>(&items[i]), static_cast<const void*>(&buffer_[(current_read_idx + i) & MASK]), sizeof(T));
    }
    
    header_->read_index.store(current_read_idx + num_to_read, std::memory_order_release);
    return num_to_read;
}

template<typename T, size_t Size>
size_t SharedRingBuffer<T, Size>::size() const {
    if (!header_) return 0;
    // Use acquire semantics for consistency if called concurrently with writes/reads
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    return static_cast<size_t>(write_idx - read_idx);
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::empty() const {
    if (!header_) return true;
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    return write_idx == read_idx;
}

template<typename T, size_t Size>
bool SharedRingBuffer<T, Size>::full() const {
    if (!header_) return false;
    const uint64_t write_idx = header_->write_index.load(std::memory_order_acquire);
    const uint64_t read_idx = header_->read_index.load(std::memory_order_acquire);
    return (write_idx - read_idx) >= Size;
}

template<typename T, size_t Size>
double SharedRingBuffer<T, Size>::utilization() const {
    if (Size == 0) return 0.0; // Avoid division by zero
    return static_cast<double>(size()) / static_cast<double>(Size);
}

// Explicit template instantiations for the message types
template class SharedRingBuffer<MarketDataMessage, 4096>;
template class SharedRingBuffer<TradingSignalMessage, 1024>;
template class SharedRingBuffer<ControlMessage, 256>;

} // namespace Alaris::IPC