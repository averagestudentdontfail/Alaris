#include "memory_pool.h"
#include <sys/mman.h>
#include <unistd.h>
#include <stdexcept>
#include <algorithm>
#include <cstring>

namespace Alaris::Core {

// Forward declaration of Block struct
struct MemoryPool::Block {
    std::byte* data;
    size_t size;
    bool is_free;
    Block* next;
};

struct MemoryPool::Impl {
    std::vector<std::byte*> chunks_;
    Block* free_list_;
    size_t chunk_size_;
    size_t total_allocated_;
    size_t total_free_;
    mutable std::mutex mutex_;
    
    Impl(size_t chunk_size) : free_list_(nullptr), chunk_size_(chunk_size), 
                              total_allocated_(0), total_free_(0) {}
};

MemoryPool::MemoryPool(size_t initial_size) 
    : pImpl_(std::make_unique<Impl>(16 * 1024 * 1024)) {
    std::cout << "MemoryPool initialized with " << initial_size << " bytes" << std::endl;
    
    // Allocate initial chunk
    add_chunk();
    
    // Pre-allocate additional memory if requested
    if (initial_size > pImpl_->chunk_size_) {
        size_t additional_chunks = (initial_size - pImpl_->chunk_size_) / pImpl_->chunk_size_;
        for (size_t i = 0; i < additional_chunks; ++i) {
            add_chunk();
        }
    }
}

MemoryPool::~MemoryPool() {
    if (pImpl_) {
        for (auto* chunk : pImpl_->chunks_) {
            munmap(chunk, pImpl_->chunk_size_);
        }
    }
}

void* MemoryPool::allocate(size_t size, size_t alignment) {
    std::lock_guard<std::mutex> lock(pImpl_->mutex_);
    
    // Align size to alignment boundary
    size_t aligned_size = (size + alignment - 1) & ~(alignment - 1);
    
    Block* block = find_best_fit(aligned_size, alignment);
    
    if (!block) {
        // Need new chunk
        add_chunk();
        block = find_best_fit(aligned_size, alignment);
        
        if (!block) {
            throw std::bad_alloc();
        }
    }
    
    // Split block if necessary
    if (block->size > aligned_size + sizeof(Block) + 64) {
        split_block(block, aligned_size);
    }
    
    block->is_free = false;
    pImpl_->total_allocated_ += block->size;
    pImpl_->total_free_ -= block->size;
    
    // Align pointer
    void* ptr = block->data;
    size_t space = block->size;
    std::align(alignment, size, ptr, space);
    
    return ptr;
}

void MemoryPool::release(void* ptr) {
    if (!ptr) return;
    
    std::lock_guard<std::mutex> lock(pImpl_->mutex_);
    
    // Find block containing this pointer
    for (auto* chunk : pImpl_->chunks_) {
        auto* block = reinterpret_cast<Block*>(chunk);
        
        while (block && block->data <= static_cast<std::byte*>(ptr) && 
               static_cast<std::byte*>(ptr) < block->data + block->size) {
            
            if (!block->is_free) {
                block->is_free = true;
                pImpl_->total_allocated_ -= block->size;
                pImpl_->total_free_ += block->size;
                
                // Attempt to merge with adjacent blocks
                merge_adjacent_blocks();
                return;
            }
            break;
        }
    }
}

void MemoryPool::reset() {
    std::lock_guard<std::mutex> lock(pImpl_->mutex_);
    
    pImpl_->free_list_ = nullptr;
    pImpl_->total_allocated_ = 0;
    pImpl_->total_free_ = 0;
    
    // Reset all chunks
    for (auto* chunk : pImpl_->chunks_) {
        auto* block = reinterpret_cast<Block*>(chunk);
        block->data = chunk + sizeof(Block);
        block->size = pImpl_->chunk_size_ - sizeof(Block);
        block->is_free = true;
        block->next = pImpl_->free_list_;
        
        pImpl_->free_list_ = block;
        pImpl_->total_free_ += block->size;
    }
}

void MemoryPool::add_chunk() {
    // Allocate memory using mmap for better control
    void* ptr = mmap(nullptr, pImpl_->chunk_size_, PROT_READ | PROT_WRITE,
                     MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    
    if (ptr == MAP_FAILED) {
        throw std::bad_alloc();
    }
    
    // Advise kernel about usage pattern
    madvise(ptr, pImpl_->chunk_size_, MADV_WILLNEED);
    
    auto* chunk = static_cast<std::byte*>(ptr);
    pImpl_->chunks_.push_back(chunk);
    
    // Create block header
    auto* block = reinterpret_cast<Block*>(chunk);
    block->data = chunk + sizeof(Block);
    block->size = pImpl_->chunk_size_ - sizeof(Block);
    block->is_free = true;
    block->next = pImpl_->free_list_;
    
    pImpl_->free_list_ = block;
    pImpl_->total_free_ += block->size;
}

MemoryPool::Block* MemoryPool::find_best_fit(size_t size, size_t alignment) {
    Block* best = nullptr;
    size_t best_size = SIZE_MAX;
    
    Block* current = pImpl_->free_list_;
    while (current) {
        if (current->is_free && current->size >= size) {
            // Check alignment
            void* ptr = current->data;
            size_t space = current->size;
            if (std::align(alignment, size, ptr, space)) {
                if (current->size < best_size) {
                    best = current;
                    best_size = current->size;
                }
            }
        }
        current = current->next;
    }
    
    return best;
}

void MemoryPool::split_block(Block* block, size_t size) {
    if (block->size <= size + sizeof(Block)) return;
    
    // Create new block from remainder
    auto* new_block = reinterpret_cast<Block*>(block->data + size);
    new_block->data = reinterpret_cast<std::byte*>(new_block) + sizeof(Block);
    new_block->size = block->size - size - sizeof(Block);
    new_block->is_free = true;
    new_block->next = block->next;
    
    // Update original block
    block->size = size;
    block->next = new_block;
}

void MemoryPool::merge_adjacent_blocks() {
    // Simple merge implementation
    for (auto* chunk : pImpl_->chunks_) {
        auto* block = reinterpret_cast<Block*>(chunk);
        
        while (block && block->next) {
            if (block->is_free && block->next->is_free) {
                // Merge blocks
                block->size += sizeof(Block) + block->next->size;
                block->next = block->next->next;
            } else {
                block = block->next;
            }
        }
    }
}

// PerCycleAllocator implementation
PerCycleAllocator::PerCycleAllocator(MemoryPool& pool) : pool_(pool) {}

PerCycleAllocator::~PerCycleAllocator() = default;

void* PerCycleAllocator::allocate(size_t size, size_t alignment) {
    return pool_.allocate(size, alignment);
}

void PerCycleAllocator::reset() {
    // In a full implementation, this would track allocations and release them
    // For now, we rely on the pool's reset mechanism
}

} // namespace Alaris::Core
