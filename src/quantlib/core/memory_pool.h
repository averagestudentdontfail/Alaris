#pragma once

#include <vector>
#include <cstddef>
#include <memory>
#include <atomic>
#include <mutex>

namespace Alaris::Core {

/**
 * @brief Simplified memory pool focused on deterministic execution
 * 
 * This implementation prioritizes:
 * - Deterministic allocation times
 * - Low fragmentation for trading workloads
 * - Simple but robust design
 * - Support for both arena and general allocation patterns
 */
class MemoryPool {
private:
    // Forward declaration for implementation details
    struct Block;
    struct Chunk;
    
    // Memory chunks allocated from OS
    std::vector<std::unique_ptr<Chunk>> chunks_;
    
    // Free lists for different size classes (powers of 2)
    static constexpr size_t NUM_SIZE_CLASSES = 16; // 64B to 2MB
    static constexpr size_t MIN_ALLOCATION_SIZE = 64;
    
    std::vector<Block*> free_lists_;
    mutable std::mutex mutex_; // For thread safety if needed
    
    // Configuration
    size_t default_chunk_size_;
    size_t total_allocated_;
    size_t total_free_;
    
    // Statistics
    mutable std::atomic<size_t> allocation_count_{0};
    mutable std::atomic<size_t> deallocation_count_{0};
    
    // Helper methods
    size_t get_size_class(size_t size) const;
    size_t get_size_for_class(size_t size_class) const;
    Block* allocate_from_size_class(size_t size_class);
    void add_chunk(size_t min_size = 0);
    void add_block_to_free_list(Block* block);
    Block* split_block(Block* block, size_t size);
    void coalesce_free_blocks();
    
public:
    explicit MemoryPool(size_t initial_size_bytes = 64 * 1024 * 1024);
    ~MemoryPool();
    
    // Non-copyable, non-movable
    MemoryPool(const MemoryPool&) = delete;
    MemoryPool& operator=(const MemoryPool&) = delete;
    MemoryPool(MemoryPool&&) = delete;
    MemoryPool& operator=(MemoryPool&&) = delete;
    
    /**
     * @brief Allocate aligned memory from the pool
     * @param size_bytes Number of bytes to allocate
     * @param alignment_bytes Required alignment (must be power of 2)
     * @return Pointer to allocated memory, nullptr on failure
     */
    void* allocate(size_t size_bytes, size_t alignment_bytes = 64);
    
    /**
     * @brief Release memory back to the pool
     * @param ptr Pointer to memory allocated by this pool
     */
    void release(void* ptr);
    
    /**
     * @brief Allocate a large chunk for arena-style allocation
     * @param size_bytes Size of arena to allocate
     * @return Pointer to arena, nullptr on failure
     */
    void* allocate_arena(size_t size_bytes);
    
    /**
     * @brief Release an entire arena back to the pool
     * @param ptr Pointer to arena allocated by allocate_arena
     */
    void release_arena(void* ptr);
    
    /**
     * @brief Reset all allocations (dangerous - invalidates all pointers)
     */
    void reset();
    
    // Statistics and monitoring
    size_t total_allocated() const { return total_allocated_; }
    size_t total_free() const { return total_free_; }
    double utilization() const;
    size_t allocation_count() const { return allocation_count_.load(); }
    size_t deallocation_count() const { return deallocation_count_.load(); }
    
    // Pre-allocation for deterministic behavior
    void pre_allocate(size_t additional_bytes);
};

/**
 * @brief Fast per-cycle allocator using arena allocation from MemoryPool
 */
class PerCycleAllocator {
private:
    MemoryPool& pool_;
    
    // Current arena
    std::byte* current_arena_;
    size_t arena_size_;
    size_t arena_used_;
    
    // List of arenas for this cycle
    std::vector<void*> allocated_arenas_;
    
    // Configuration
    size_t default_arena_size_;
    
    // Statistics
    size_t allocations_this_cycle_;
    size_t bytes_allocated_this_cycle_;
    
    bool allocate_new_arena(size_t min_size);
    
public:
    explicit PerCycleAllocator(MemoryPool& pool, size_t default_arena_size = 4 * 1024 * 1024);
    ~PerCycleAllocator();
    
    // Non-copyable, non-movable
    PerCycleAllocator(const PerCycleAllocator&) = delete;
    PerCycleAllocator& operator=(const PerCycleAllocator&) = delete;
    PerCycleAllocator(PerCycleAllocator&&) = delete;
    PerCycleAllocator& operator=(PerCycleAllocator&&) = delete;
    
    /**
     * @brief Allocate memory for current cycle (very fast)
     * @param size_bytes Number of bytes to allocate
     * @param alignment_bytes Required alignment
     * @return Pointer to allocated memory, nullptr on failure
     */
    void* allocate(size_t size_bytes, size_t alignment_bytes = 64);
    
    /**
     * @brief Reset allocator, releasing all memory for this cycle
     */
    void reset();
    
    // Statistics
    size_t get_allocation_count_this_cycle() const { return allocations_this_cycle_; }
    size_t get_bytes_allocated_this_cycle() const { return bytes_allocated_this_cycle_; }
    bool has_space_for(size_t size_bytes, size_t alignment_bytes) const;
};

} // namespace Alaris::Core