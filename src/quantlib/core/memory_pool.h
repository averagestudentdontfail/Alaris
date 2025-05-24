#pragma once

#include <vector>
#include <cstddef>
#include <memory>
#include <atomic>
#include <mutex>
#include <iostream>

namespace Alaris::Core {

class MemoryPool {
private:
    // Forward declaration of internal types
    struct Block;
    struct Impl;
    
    std::unique_ptr<Impl> pImpl_;
    
    // Private methods
    void add_chunk();
    Block* find_best_fit(size_t size, size_t alignment);
    void split_block(Block* block, size_t size);
    void merge_adjacent_blocks();
    
public:
    explicit MemoryPool(size_t initial_size_bytes = 64 * 1024 * 1024);
    ~MemoryPool();
    
    // Non-copyable
    MemoryPool(const MemoryPool&) = delete;
    MemoryPool& operator=(const MemoryPool&) = delete;
    
    // Allocate aligned memory
    void* allocate(size_t size_bytes, size_t alignment_bytes = 64);
    
    // Release memory back to pool
    void release(void* ptr);
    
    // Reset pool to initial state (clears all allocations)
    void reset();
    
    // Statistics
    size_t total_allocated() const { return 0; } // Simplified for now
    size_t total_free() const { return 0; }
    double utilization() const { return 0.0; }
    
    // Pre-allocate memory to avoid runtime allocation
    void pre_allocate(size_t additional_bytes) { add_chunk(); }
};

// Lock-free per-cycle allocator for deterministic execution
class PerCycleAllocator {
private:
    MemoryPool& pool_;
    
public:
    explicit PerCycleAllocator(MemoryPool& pool);
    ~PerCycleAllocator();
    
    // Non-copyable
    PerCycleAllocator(const PerCycleAllocator&) = delete;
    PerCycleAllocator& operator=(const PerCycleAllocator&) = delete;
    
    // Allocate memory for current cycle
    void* allocate(size_t size_bytes, size_t alignment_bytes = 64);
    
    // Reset allocator at end of cycle - releases all allocations
    void reset();
    
    // Statistics
    size_t allocation_count() const { return 0; }
    bool has_space() const { return true; }
};

} // namespace Alaris::Core
