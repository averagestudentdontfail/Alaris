#include "memory_pool.h"
#include <sys/mman.h>
#include <unistd.h>
#include <stdexcept>
#include <algorithm>
#include <cstring>
#include <cassert>

namespace Alaris::Core {

// Block header for tracking allocations
struct MemoryPool::Block {
    size_t size;           // Size of usable data area
    size_t size_class;     // Which size class this belongs to
    bool is_free;          // Whether this block is free
    Block* next_free;      // Next block in free list (if free)
    
    // Magic number for debugging
    static constexpr uint32_t MAGIC = 0xDEADBEEF;
    uint32_t magic;
    
    Block(size_t sz, size_t sc) : size(sz), size_class(sc), is_free(true), 
                                  next_free(nullptr), magic(MAGIC) {}
    
    std::byte* data() { return reinterpret_cast<std::byte*>(this + 1); }
    const std::byte* data() const { return reinterpret_cast<const std::byte*>(this + 1); }
    
    bool is_valid() const { return magic == MAGIC; }
};

// Chunk represents a large allocation from the OS
struct MemoryPool::Chunk {
    std::byte* memory;
    size_t size;
    size_t used;
    bool is_arena_chunk; // True if this chunk is for arena allocation
    
    Chunk(size_t sz, bool is_arena = false) : size(sz), used(0), is_arena_chunk(is_arena) {
        // Use mmap for better control and huge page support
        memory = static_cast<std::byte*>(
            mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0)
        );
        
        if (memory == MAP_FAILED) {
            throw std::bad_alloc();
        }
        
        // Advise kernel about usage pattern
        madvise(memory, size, MADV_WILLNEED);
    }
    
    ~Chunk() {
        if (memory && memory != MAP_FAILED) {
            munmap(memory, size);
        }
    }
    
    // Non-copyable, movable
    Chunk(const Chunk&) = delete;
    Chunk& operator=(const Chunk&) = delete;
    
    Chunk(Chunk&& other) noexcept 
        : memory(other.memory), size(other.size), used(other.used), 
          is_arena_chunk(other.is_arena_chunk) {
        other.memory = nullptr;
        other.size = 0;
        other.used = 0;
    }
    
    std::byte* allocate(size_t bytes, size_t alignment) {
        // Align the current position
        size_t current_pos = (used + alignment - 1) & ~(alignment - 1);
        
        if (current_pos + bytes > size) {
            return nullptr; // Not enough space
        }
        
        std::byte* result = memory + current_pos;
        used = current_pos + bytes;
        return result;
    }
    
    bool contains(void* ptr) const {
        return ptr >= memory && ptr < memory + size;
    }
};

// Utility functions
static inline size_t align_up(size_t value, size_t alignment) {
    return (value + alignment - 1) & ~(alignment - 1);
}

static inline bool is_power_of_two(size_t value) {
    return value != 0 && (value & (value - 1)) == 0;
}

MemoryPool::MemoryPool(size_t initial_size_bytes) 
    : default_chunk_size_(16 * 1024 * 1024), // 16MB default chunks
      total_allocated_(0), 
      total_free_(0) {
    
    // Initialize free lists
    free_lists_.resize(NUM_SIZE_CLASSES, nullptr);
    
    // Allocate initial chunk if requested
    if (initial_size_bytes > 0) {
        add_chunk(initial_size_bytes);
    }
}

MemoryPool::~MemoryPool() {
    // Chunks will be automatically cleaned up by their destructors
}

size_t MemoryPool::get_size_class(size_t size) const {
    if (size < MIN_ALLOCATION_SIZE) {
        return 0;
    }
    
    // Find the size class (power of 2)
    size_t adjusted_size = std::max(size, MIN_ALLOCATION_SIZE);
    size_t size_class = 0;
    size_t class_size = MIN_ALLOCATION_SIZE;
    
    while (class_size < adjusted_size && size_class < NUM_SIZE_CLASSES - 1) {
        class_size *= 2;
        size_class++;
    }
    
    return size_class;
}

size_t MemoryPool::get_size_for_class(size_t size_class) const {
    return MIN_ALLOCATION_SIZE << size_class;
}

void MemoryPool::add_chunk(size_t min_size) {
    size_t chunk_size = std::max(min_size, default_chunk_size_);
    
    // Align to page boundary
    size_t page_size = sysconf(_SC_PAGE_SIZE);
    chunk_size = align_up(chunk_size, page_size);
    
    auto chunk = std::make_unique<Chunk>(chunk_size);
    
    // Create initial free block covering most of the chunk
    // Leave some space at the beginning for chunk metadata alignment
    size_t block_offset = align_up(0, alignof(Block));
    size_t available_size = chunk_size - block_offset - sizeof(Block);
    
    Block* initial_block = reinterpret_cast<Block*>(chunk->memory + block_offset);
    new (initial_block) Block(available_size, get_size_class(available_size));
    
    add_block_to_free_list(initial_block);
    
    chunk->used = block_offset + sizeof(Block) + available_size;
    total_free_ += available_size;
    
    chunks_.emplace_back(std::move(chunk));
}

void MemoryPool::add_block_to_free_list(Block* block) {
    assert(block && block->is_valid());
    
    block->is_free = true;
    size_t size_class = std::min(block->size_class, NUM_SIZE_CLASSES - 1);
    
    block->next_free = free_lists_[size_class];
    free_lists_[size_class] = block;
}

Block* MemoryPool::allocate_from_size_class(size_t size_class) {
    // Try exact size class first
    if (size_class < NUM_SIZE_CLASSES && free_lists_[size_class]) {
        Block* block = free_lists_[size_class];
        free_lists_[size_class] = block->next_free;
        block->next_free = nullptr;
        block->is_free = false;
        return block;
    }
    
    // Try larger size classes
    for (size_t larger_class = size_class + 1; larger_class < NUM_SIZE_CLASSES; ++larger_class) {
        if (free_lists_[larger_class]) {
            Block* block = free_lists_[larger_class];
            free_lists_[larger_class] = block->next_free;
            
            // Try to split the block if it's much larger than needed
            size_t needed_size = get_size_for_class(size_class);
            if (block->size >= needed_size + sizeof(Block) + MIN_ALLOCATION_SIZE) {
                return split_block(block, needed_size);
            }
            
            block->next_free = nullptr;
            block->is_free = false;
            return block;
        }
    }
    
    return nullptr;
}

Block* MemoryPool::split_block(Block* block, size_t size) {
    assert(block && block->is_valid());
    assert(block->size >= size + sizeof(Block) + MIN_ALLOCATION_SIZE);
    
    // Create new block from remainder
    std::byte* split_pos = block->data() + size;
    size_t remainder_size = block->size - size - sizeof(Block);
    
    Block* new_block = reinterpret_cast<Block*>(split_pos);
    new (new_block) Block(remainder_size, get_size_class(remainder_size));
    
    // Update original block
    block->size = size;
    block->size_class = get_size_class(size);
    block->is_free = false;
    
    // Add remainder to free list
    add_block_to_free_list(new_block);
    
    return block;
}

void* MemoryPool::allocate(size_t size_bytes, size_t alignment_bytes) {
    if (size_bytes == 0) {
        return nullptr;
    }
    
    // Ensure alignment is power of 2
    if (!is_power_of_two(alignment_bytes)) {
        alignment_bytes = 64; // Default alignment
    }
    
    std::lock_guard<std::mutex> lock(mutex_);
    
    // Account for alignment padding
    size_t total_size = size_bytes + alignment_bytes;
    size_t size_class = get_size_class(total_size);
    
    Block* block = allocate_from_size_class(size_class);
    
    if (!block) {
        // Need new chunk
        size_t chunk_size = std::max(total_size + sizeof(Block), default_chunk_size_);
        add_chunk(chunk_size);
        
        block = allocate_from_size_class(size_class);
        if (!block) {
            return nullptr; // Still no memory available
        }
    }
    
    // Align the data pointer
    std::byte* data_ptr = block->data();
    std::byte* aligned_ptr = reinterpret_cast<std::byte*>(
        align_up(reinterpret_cast<uintptr_t>(data_ptr), alignment_bytes)
    );
    
    total_allocated_ += block->size;
    total_free_ = (total_free_ >= block->size) ? total_free_ - block->size : 0;
    allocation_count_.fetch_add(1);
    
    return aligned_ptr;
}

void MemoryPool::release(void* ptr) {
    if (!ptr) {
        return;
    }
    
    std::lock_guard<std::mutex> lock(mutex_);
    
    // Find the block header
    // This is a simplified search - in a production system you'd want
    // a more efficient way to find the block header from the data pointer
    Block* block = nullptr;
    
    for (const auto& chunk : chunks_) {
        if (chunk->contains(ptr)) {
            // Scan the chunk for the block containing this pointer
            std::byte* chunk_ptr = chunk->memory;
            std::byte* chunk_end = chunk->memory + chunk->used;
            
            while (chunk_ptr < chunk_end) {
                Block* candidate = reinterpret_cast<Block*>(chunk_ptr);
                if (candidate->is_valid() && 
                    ptr >= candidate->data() && 
                    ptr < candidate->data() + candidate->size) {
                    block = candidate;
                    break;
                }
                
                // Move to next potential block location
                chunk_ptr += sizeof(Block) + candidate->size;
            }
            break;
        }
    }
    
    if (block && !block->is_free) {
        total_allocated_ = (total_allocated_ >= block->size) ? total_allocated_ - block->size : 0;
        total_free_ += block->size;
        deallocation_count_.fetch_add(1);
        
        add_block_to_free_list(block);
        
        // Periodically coalesce to reduce fragmentation
        if (deallocation_count_.load() % 100 == 0) {
            coalesce_free_blocks();
        }
    }
}

void* MemoryPool::allocate_arena(size_t size_bytes) {
    if (size_bytes == 0) {
        return nullptr;
    }
    
    std::lock_guard<std::mutex> lock(mutex_);
    
    // For arena allocation, create a dedicated chunk
    try {
        auto chunk = std::make_unique<Chunk>(size_bytes, true);
        void* result = chunk->memory;
        chunk->used = size_bytes;
        
        total_allocated_ += size_bytes;
        allocation_count_.fetch_add(1);
        
        chunks_.emplace_back(std::move(chunk));
        return result;
    } catch (const std::bad_alloc&) {
        return nullptr;
    }
}

void MemoryPool::release_arena(void* ptr) {
    if (!ptr) {
        return;
    }
    
    std::lock_guard<std::mutex> lock(mutex_);
    
    // Find and remove the arena chunk
    auto it = std::find_if(chunks_.begin(), chunks_.end(),
        [ptr](const std::unique_ptr<Chunk>& chunk) {
            return chunk->is_arena_chunk && chunk->memory == ptr;
        });
    
    if (it != chunks_.end()) {
        total_allocated_ = (total_allocated_ >= (*it)->size) ? total_allocated_ - (*it)->size : 0;
        deallocation_count_.fetch_add(1);
        chunks_.erase(it);
    }
}

void MemoryPool::reset() {
    std::lock_guard<std::mutex> lock(mutex_);
    
    chunks_.clear();
    std::fill(free_lists_.begin(), free_lists_.end(), nullptr);
    
    total_allocated_ = 0;
    total_free_ = 0;
    allocation_count_.store(0);
    deallocation_count_.store(0);
}

void MemoryPool::coalesce_free_blocks() {
    // Simple coalescing - in a production system this would be more sophisticated
    // For now, just rebuild the free lists periodically
    std::fill(free_lists_.begin(), free_lists_.end(), nullptr);
    total_free_ = 0;
    
    for (const auto& chunk : chunks_) {
        if (chunk->is_arena_chunk) continue;
        
        std::byte* chunk_ptr = chunk->memory;
        std::byte* chunk_end = chunk->memory + chunk->used;
        
        while (chunk_ptr < chunk_end) {
            Block* block = reinterpret_cast<Block*>(chunk_ptr);
            if (block->is_valid() && block->is_free) {
                add_block_to_free_list(block);
                total_free_ += block->size;
            }
            chunk_ptr += sizeof(Block) + block->size;
        }
    }
}

double MemoryPool::utilization() const {
    size_t total_capacity = total_allocated_ + total_free_;
    return total_capacity > 0 ? static_cast<double>(total_allocated_) / total_capacity : 0.0;
}

void MemoryPool::pre_allocate(size_t additional_bytes) {
    std::lock_guard<std::mutex> lock(mutex_);
    add_chunk(additional_bytes);
}

// PerCycleAllocator implementation
PerCycleAllocator::PerCycleAllocator(MemoryPool& pool, size_t default_arena_size)
    : pool_(pool), 
      current_arena_(nullptr), 
      arena_size_(0), 
      arena_used_(0),
      default_arena_size_(default_arena_size),
      allocations_this_cycle_(0),
      bytes_allocated_this_cycle_(0) {
}

PerCycleAllocator::~PerCycleAllocator() {
    reset();
}

bool PerCycleAllocator::allocate_new_arena(size_t min_size) {
    size_t arena_size = std::max(min_size, default_arena_size_);
    
    void* new_arena = pool_.allocate_arena(arena_size);
    if (!new_arena) {
        return false;
    }
    
    allocated_arenas_.push_back(new_arena);
    current_arena_ = static_cast<std::byte*>(new_arena);
    arena_size_ = arena_size;
    arena_used_ = 0;
    
    return true;
}

void* PerCycleAllocator::allocate(size_t size_bytes, size_t alignment_bytes) {
    if (size_bytes == 0) {
        return nullptr;
    }
    
    // Ensure alignment is power of 2
    if (!is_power_of_two(alignment_bytes)) {
        alignment_bytes = 64;
    }
    
    // Calculate aligned position
    size_t aligned_pos = align_up(arena_used_, alignment_bytes);
    size_t total_needed = aligned_pos - arena_used_ + size_bytes;
    
    // Check if current arena has space
    if (!current_arena_ || aligned_pos + size_bytes > arena_size_) {
        if (!allocate_new_arena(size_bytes + alignment_bytes)) {
            return nullptr;
        }
        aligned_pos = 0;
        total_needed = size_bytes;
    }
    
    void* result = current_arena_ + aligned_pos;
    arena_used_ = aligned_pos + size_bytes;
    
    allocations_this_cycle_++;
    bytes_allocated_this_cycle_ += size_bytes;
    
    return result;
}

void PerCycleAllocator::reset() {
    // Release all arenas back to the pool
    for (void* arena : allocated_arenas_) {
        pool_.release_arena(arena);
    }
    
    allocated_arenas_.clear();
    current_arena_ = nullptr;
    arena_size_ = 0;
    arena_used_ = 0;
    allocations_this_cycle_ = 0;
    bytes_allocated_this_cycle_ = 0;
}

bool PerCycleAllocator::has_space_for(size_t size_bytes, size_t alignment_bytes) const {
    if (!current_arena_) {
        return false;
    }
    
    size_t aligned_pos = align_up(arena_used_, alignment_bytes);
    return aligned_pos + size_bytes <= arena_size_;
}

} // namespace Alaris::Core