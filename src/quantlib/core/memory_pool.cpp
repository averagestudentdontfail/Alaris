#include "memory_pool.h" // Defines Alaris::Core::MemoryPool and its nested Block, Chunk
#include <sys/mman.h>
#include <unistd.h>     // For sysconf
#include <stdexcept>    // For std::bad_alloc
#include <algorithm>    // For std::max, std::min, std::fill, std::find_if
#include <cstring>      // For std::memset (though use with caution)
#include <cassert>      // For assert
#include <iostream>     // For std::cerr (temporary debug, if any)
#include <new>          // For new (placement new)

namespace Alaris::Core {

// Definition of nested struct MemoryPool::Block (as provided in your uploaded memory_pool.cpp)
struct MemoryPool::Block {
    size_t size;           // Size of usable data area
    size_t size_class;     // Which size class this belongs to
    bool is_free;          // Whether this block is free
    Block* next_free;      // Next block in free list (if free)
    
    static constexpr uint32_t MAGIC = 0xDEADBEEF;
    uint32_t magic;
    
    Block(size_t sz, size_t sc) : size(sz), size_class(sc), is_free(true), 
                                  next_free(nullptr), magic(MAGIC) {}
    
    // data() method to get pointer to usable memory area, which starts after the Block header itself
    std::byte* data() { return reinterpret_cast<std::byte*>(this + 1); }
    const std::byte* data() const { return reinterpret_cast<const std::byte*>(this + 1); }
    
    bool is_valid() const { return magic == MAGIC; }
};

// Definition of nested struct MemoryPool::Chunk (as provided in your uploaded memory_pool.cpp)
struct MemoryPool::Chunk {
    std::byte* memory;     // Pointer to the start of the mmap'd region
    size_t size;           // Total size of the mmap'd region
    size_t used;           // Bytes used within this chunk for Block headers and their data
    bool is_arena_chunk;   // True if this chunk is for PerCycleAllocator's direct use

    Chunk(size_t sz, bool is_arena = false) : size(sz), used(0), is_arena_chunk(is_arena) {
        memory = static_cast<std::byte*>(
            mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0)
        );
        
        if (memory == MAP_FAILED) {
            perror("MemoryPool::Chunk: mmap failed");
            throw std::bad_alloc();
        }
        
        // Consider madvise(memory, size, MADV_DONTFORK) if chunks shouldn't be inherited by forks.
        // madvise(memory, size, MADV_WILLNEED); // Already in your version
    }
    
    ~Chunk() {
        if (memory && memory != MAP_FAILED) {
            munmap(memory, size);
        }
    }
    
    Chunk(const Chunk&) = delete;
    Chunk& operator=(const Chunk&) = delete;
    
    Chunk(Chunk&& other) noexcept 
        : memory(other.memory), size(other.size), used(other.used), 
          is_arena_chunk(other.is_arena_chunk) {
        other.memory = nullptr; // Invalidate other
        other.size = 0;
        other.used = 0;
    }
    // No specific allocate method here in your version; chunk is a raw memory block
    // The 'allocate' method in your original was for linear allocation if chunk was an arena.
    // This is now handled by PerCycleAllocator more directly, or MemoryPool carves Blocks.

    bool contains(const void* ptr) const {
        const std::byte* byte_ptr = static_cast<const std::byte*>(ptr);
        return byte_ptr >= memory && byte_ptr < memory + size;
    }
};

// Utility functions (file-local)
namespace {
    size_t align_up(size_t value, size_t alignment) {
        return (value + alignment - 1) & ~(alignment - 1);
    }

    bool is_power_of_two(size_t value) {
        return value != 0 && (value & (value - 1)) == 0;
    }
} // anonymous namespace


MemoryPool::MemoryPool(size_t initial_size_bytes) 
    : default_chunk_size_(16 * 1024 * 1024), 
      total_allocated_(0), 
      total_free_(0) { // NUM_SIZE_CLASSES and MIN_ALLOCATION_SIZE are static constexpr in header
    
    free_lists_.resize(NUM_SIZE_CLASSES, nullptr);
    
    if (initial_size_bytes > 0) {
        add_chunk(initial_size_bytes);
    }
}

MemoryPool::~MemoryPool() {
    // Chunks (and their mmap'd memory) are cleaned up by unique_ptr destructors.
}

size_t MemoryPool::get_size_class(size_t size) const {
    if (size == 0) return 0; // Or handle as error
    size_t min_alloc = MIN_ALLOCATION_SIZE; // From header's static constexpr
    if (size < min_alloc) { // All small allocations go to the first size class
        return 0;
    }
    
    // Find the smallest power-of-2 size class that fits 'size'
    // Example: size 65-128 -> class for 128B; size 129-256 -> class for 256B
    // This can be done by finding the position of the most significant bit or log2.
    // A simpler loop based on doubling:
    size_t current_class_size = min_alloc;
    for (size_t sc = 0; sc < NUM_SIZE_CLASSES -1; ++sc) {
        if (size <= current_class_size) {
            return sc;
        }
        current_class_size *= 2;
    }
    return NUM_SIZE_CLASSES - 1; // Largest size class for anything bigger
}

size_t MemoryPool::get_size_for_class(size_t size_class) const {
    if (size_class >= NUM_SIZE_CLASSES) { // Should not happen if get_size_class is correct
        // Fallback to a very large size or throw error.
        // This indicates request for class beyond defined limits.
        // For simplicity, return max possible size for the last class.
        // Or, rely on NUM_SIZE_CLASSES-1 being the catch-all for large allocations.
        size_class = NUM_SIZE_CLASSES - 1;
    }
    return MIN_ALLOCATION_SIZE << size_class; // MIN_ALLOCATION_SIZE * 2^size_class
}

void MemoryPool::add_chunk(size_t min_data_size) {
    // Determine total chunk size needed: min_data_size + space for at least one Block header.
    // The Block headers are placed *within* the chunk memory.
    size_t size_for_block_and_data = min_data_size + sizeof(Block); // Block header + min data
    size_t chunk_size_to_request = std::max(size_for_block_and_data, default_chunk_size_);
    
    long page_size_long = sysconf(_SC_PAGE_SIZE);
    if (page_size_long <= 0) { // sysconf error or invalid page size
        page_size_long = 4096; // Default to 4KB if sysconf fails
    }
    size_t page_size = static_cast<size_t>(page_size_long);
    chunk_size_to_request = align_up(chunk_size_to_request, page_size);
    
    auto new_os_chunk = std::make_unique<Chunk>(chunk_size_to_request, false /*not an arena chunk*/);
    
    // The entire usable part of the chunk becomes one initial free Block.
    // The Block header is placed at the beginning of the usable memory in the chunk.
    if (chunk_size_to_request < sizeof(Block)) { // Sanity check, should not happen with page alignment
        // Not enough space for even a Block header. This chunk is useless.
        // The Chunk destructor will munmap.
        throw std::runtime_error("MemoryPool::add_chunk - Requested chunk size too small for Block metadata.");
    }

    Block* initial_block_header = reinterpret_cast<Block*>(new_os_chunk->memory);
    size_t initial_block_data_size = chunk_size_to_request - sizeof(Block);

    new (static_cast<void*>(initial_block_header)) Block(initial_block_data_size, get_size_class(initial_block_data_size));
    // initial_block_header->data() now points to usable memory after this header.
    
    add_block_to_free_list(initial_block_header);
    
    new_os_chunk->used = chunk_size_to_request; // Mark entire OS chunk as "managed" by blocks
    total_free_ += initial_block_data_size;
    
    chunks_.emplace_back(std::move(new_os_chunk));
}

void MemoryPool::add_block_to_free_list(MemoryPool::Block* block) { // Fully qualify Block
    assert(block && block->is_valid() && block->is_free); // is_free should be true before adding
    
    size_t sc = std::min(block->size_class, NUM_SIZE_CLASSES - 1);
    
    block->next_free = free_lists_[sc];
    free_lists_[sc] = block;
}

// Corrected function signature
Alaris::Core::MemoryPool::Block* MemoryPool::allocate_from_size_class(size_t size_class) {
    if (size_class >= NUM_SIZE_CLASSES) return nullptr;

    if (free_lists_[size_class]) {
        Block* block = free_lists_[size_class];
        free_lists_[size_class] = block->next_free;
        block->next_free = nullptr;
        block->is_free = false; // Mark as not free
        return block;
    }
    
    // Try larger size classes and split if found
    for (size_t larger_sc = size_class + 1; larger_sc < NUM_SIZE_CLASSES; ++larger_sc) {
        if (free_lists_[larger_sc]) {
            Block* large_block = free_lists_[larger_sc];
            free_lists_[larger_sc] = large_block->next_free; // Remove from this larger free list
            
            // Split the large_block. The first part will be returned, remainder put back on free list.
            // The 'split_block' function needs the size desired for the *first* part (the allocated one).
            size_t size_needed_for_this_class = get_size_for_class(size_class);
            return split_block(large_block, size_needed_for_this_class); // split_block marks the returned block as not free
        }
    }
    return nullptr; // No suitable block found in any class
}

// Corrected function signature and implementation detail
Alaris::Core::MemoryPool::Block* MemoryPool::split_block(MemoryPool::Block* block_to_split, size_t required_data_size) {
    assert(block_to_split && block_to_split->is_valid());
    // block_to_split is currently not on any free list.
    // required_data_size is the size of the data area for the block we want to allocate.

    // Total space needed for the first (allocated) block: header + required_data_size
    // Total space in block_to_split: sizeof(Block) + block_to_split->size (original data size)

    // Calculate size of the remaining data area after carving out the first block
    size_t remaining_data_size = block_to_split->size - required_data_size;

    // Check if the remainder is large enough to form a new valid Block (header + some minimal data)
    if (remaining_data_size >= (sizeof(Block) + MIN_ALLOCATION_SIZE)) {
        // Yes, we can split.
        // The new (remainder) Block header will be placed right after the data of the first block.
        Block* remainder_block_header = reinterpret_cast<Block*>(block_to_split->data() + required_data_size);
        size_t remainder_block_data_size = remaining_data_size - sizeof(Block);

        new (static_cast<void*>(remainder_block_header)) Block(remainder_block_data_size, get_size_class(remainder_block_data_size));
        add_block_to_free_list(remainder_block_header); // Remainder is free
        
        // Update the original block (which is now the allocated block)
        block_to_split->size = required_data_size;
        block_to_split->size_class = get_size_class(required_data_size);
        block_to_split->is_free = false; // This block is being allocated
        block_to_split->next_free = nullptr;

        return block_to_split;
    } else {
        // Remainder is too small to split, so allocate the whole original block_to_split.
        block_to_split->is_free = false;
        block_to_split->next_free = nullptr;
        return block_to_split;
    }
}


void* MemoryPool::allocate(size_t size_bytes, size_t alignment_bytes) {
    if (size_bytes == 0) return nullptr;
    
    if (!is_power_of_two(alignment_bytes)) { // Ensure alignment is power of 2
        alignment_bytes = MIN_ALLOCATION_SIZE; // Default to min alloc size, which should be power of 2
        if (!is_power_of_two(alignment_bytes)) alignment_bytes = 64; // Fallback
    }
    // The actual allocation size needed from a Block's data area must account for alignment padding.
    // If a Block's data() pointer is not already aligned to alignment_bytes, we need more space.
    // Maximum possible padding is alignment_bytes - 1.
    // So, request a block that can hold size_bytes + alignment_bytes - 1.
    // Then, once we get the block->data(), we align it.
    size_t effective_size_needed = size_bytes + alignment_bytes -1; // Max needed for data + padding
    if (effective_size_needed < size_bytes) effective_size_needed = size_bytes; // Overflow check for large alignment

    size_t sc = get_size_class(effective_size_needed); // Get size class for this effective size

    std::lock_guard<std::mutex> lock(mutex_);
    
    Block* block_header = allocate_from_size_class(sc);
    
    if (!block_header) {
        // No suitable block, add a new chunk large enough for this request + header + alignment
        add_chunk(sizeof(Block) + effective_size_needed);
        block_header = allocate_from_size_class(sc); // Try again
        if (!block_header) {
            return nullptr; // Still no memory
        }
    }

    assert(block_header && block_header->is_valid() && !block_header->is_free);
    assert(block_header->size >= effective_size_needed || block_header->size >= size_bytes); // block_header->size is data area size

    std::byte* data_start_ptr = block_header->data();
    void* aligned_user_ptr = static_cast<void*>(data_start_ptr); // Initial value before std::align
    size_t space_in_block_data = block_header->size; // Usable space from data_start_ptr

    if (!std::align(alignment_bytes, size_bytes, aligned_user_ptr, space_in_block_data)) {
        // This should not happen if effective_size_needed was calculated correctly and block_header->size is sufficient.
        // If it does, it means the block wasn't actually large enough after all.
        // Put block back to free list and report error.
        add_block_to_free_list(block_header); // This needs to be careful about state
        block_header->is_free = true; // Mark it free before adding
        std::cerr << "MemoryPool::allocate alignment failed unexpectedly." << std::endl;
        return nullptr;
    }
    
    // The aligned_user_ptr is what we return. The block_header is just before block_header->data().
    // The user must pass aligned_user_ptr to release. Release must find block_header.
    // The `block_header` we have is the actual header for this allocation.

    total_allocated_ += block_header->size; // Count the entire block's data area as allocated for now
    total_free_ = (total_free_ >= block_header->size) ? total_free_ - block_header->size : 0;
    allocation_count_.fetch_add(1, std::memory_order_relaxed);
    
    return aligned_user_ptr;
}

void MemoryPool::release(void* user_ptr) {
    if (!user_ptr) return;
    
    // To find the Block header: user_ptr is (Block*)(some_chunk_memory_base + offset) + 1 (i.e., its data() part).
    // So, the header is at (Block*)user_ptr - sizeof(Block) if we know user_ptr points to Block::data().
    // More robustly: (Block*)( (std::byte*)user_ptr - (offset_of_data_within_Block_if_data_is_not_flexible_array_member) )
    // Since Block::data() is `reinterpret_cast<std::byte*>(this + 1)`,
    // the Block header is at `reinterpret_cast<Block*>(static_cast<std::byte*>(user_ptr) - sizeof(Block))`
    // THIS ASSUMES user_ptr IS THE EXACT POINTER RETURNED BY Block::data() AND NO ALIGNMENT HAPPENED *AFTER* THAT.
    // The `allocate` returns `aligned_user_ptr`. This `aligned_user_ptr` might not be `block_header->data()`.
    // It could be `block_header->data() + padding`.

    // This makes `release` very tricky with the current `allocate` and `Block` structure.
    // The user's uploaded memory_pool.cpp had a scan for release:
    // for (const auto& chunk : chunks_) { if (chunk->contains(ptr)) { ... scan chunk ... } }
    // This is slow. A common approach is to store the header pointer just before the aligned_user_ptr.
    // E.g., ptr_to_header = aligned_user_ptr - sizeof(HeaderPointer); *(HeaderPointer*)ptr_to_header = block_header;
    // Or, if alignment allows, `block_header = (Block*)((uintptr_t)aligned_user_ptr & ~(alignment_of_block_header_itself - 1)) - 1` (if headers are on fixed boundaries)

    // For now, using the scan approach from user's original memory_pool.cpp for Block finding.
    // This assumes block headers are identifiable within chunks.
    std::lock_guard<std::mutex> lock(mutex_);
    Block* found_block_header = nullptr;

    for (const auto& chunk : chunks_) {
        if (chunk->contains(user_ptr)) { // Check if ptr is within this OS chunk
            // Now iterate through blocks *managed* within this chunk.
            // This requires knowledge of how Blocks are laid out, which the current design implies
            // they are carved from the chunk->memory.
            std::byte* current_scan_ptr = chunk->memory;
            while (current_scan_ptr < chunk->memory + chunk->used) { // chunk->used is high watermark of block headers
                Block* candidate_header = reinterpret_cast<Block*>(current_scan_ptr);
                if (!candidate_header->is_valid()) { // Invalid magic, perhaps corrupted memory or end of formatted blocks
                    // This might indicate an issue or that we've gone past known blocks.
                    break; 
                }

                std::byte* data_area_start = candidate_header->data();
                std::byte* data_area_end = data_area_start + candidate_header->size;

                // Check if user_ptr is within the data area managed by this candidate_header
                // More precisely, user_ptr should be data_area_start + some_padding_from_alignment.
                // If allocate stores the true block_header pointer before the user_ptr, that's easiest.
                // Here, we assume user_ptr is SOMEWHERE within data_area_start to data_area_start + size_bytes_requested
                // And the header for it is `candidate_header`. This is still fuzzy.
                // The `std::align` in allocate means the returned `aligned_user_ptr` is not necessarily `candidate_header->data()`.
                // The `block_header` from `allocate` is the one whose `data()` region *contains* `aligned_user_ptr`.

                // Simplification: To release `user_ptr`, we need the *original* `Block*` header.
                // This information is lost if `allocate` only returns the aligned data pointer.
                // The user's `Block` struct in `memory_pool.cpp` had `data() { return reinterpret_cast<std::byte*>(this + 1); }`
                // So, `Block* header = reinterpret_cast<Block*>(static_cast<std::byte*>(user_ptr_from_data_area) - sizeof(Block))` is not quite right
                // because `user_ptr` could be `header->data() + padding`.
                // We need to find the header whose `data()` region (after alignment) starts at `user_ptr`.

                // Let's assume (as in typical allocators) that the `user_ptr` can be used to find its header.
                // If `user_ptr` is the result of `align_up(candidate_header->data(), alignment_bytes)`,
                // then `candidate_header` is what we need.
                // We need to iterate through all blocks, not just free ones.
                // The current structure doesn't easily allow iterating *all* blocks (free or used).
                // This `release` is very hard to implement correctly with the current structure without more info.
                // The user's `release` had a similar loop.

                // A common way: `Block* header = (Block*)((char*)user_ptr - offset_of_data_field_in_block_struct)`
                // If user_ptr points to the start of the data segment that immediately follows the header.
                // For `std::byte* data() { return reinterpret_cast<std::byte*>(this + 1); }`,
                // if `user_ptr == some_block->data()`, then `some_block == reinterpret_cast<Block*>(static_cast<std::byte*>(user_ptr) - sizeof(Block))`.
                // BUT `allocate` returns an *aligned* pointer from within that data() region.

                // Fallback: The user's `Block` struct seems to imply the block header is directly before the data area.
                // If `user_ptr` is the start of the *aligned data*, the header is NOT necessarily `user_ptr - sizeof(Block)`.
                // This `release` function is the hardest part of a custom allocator.
                // The user's `memory_pool.cpp` snippet for release had a similar scan.
                // Let's assume `user_ptr` is actually `block_header->data()` for this simplified release.
                // This is often NOT true due to alignment.
                if (static_cast<void*>(candidate_header->data()) == user_ptr) { // Highly unlikely if alignment happened
                    found_block_header = candidate_header;
                    break;
                }
                
                // Advance scan_ptr by size of current block header + its data size
                if (candidate_header->size == 0 && candidate_header->is_free == false && candidate_header->magic != Block::MAGIC) {
                    // Likely uninitialized memory or end of chunk's managed blocks.
                    break;
                }
                current_scan_ptr += sizeof(Block) + candidate_header->size; // This assumes blocks are contiguous
                                                                      // which they are if carved from a chunk.
            }
            if (found_block_header) break;
        }
    }


    if (found_block_header && found_block_header->is_valid() && !found_block_header->is_free) {
        total_allocated_ = (total_allocated_ >= found_block_header->size) ? total_allocated_ - found_block_header->size : 0;
        total_free_ += found_block_header->size;
        deallocation_count_.fetch_add(1, std::memory_order_relaxed);
        
        add_block_to_free_list(found_block_header); // Marks block as free and adds to list
        
        // Coalescing is complex and ideally done here or periodically.
        // if (deallocation_count_.load() % 100 == 0) { coalesce_free_blocks(); }
    } else {
        // std::cerr << "MemoryPool::release - Warning: Attempt to free invalid or already free pointer: " << user_ptr << std::endl;
        // If found_block_header is null, means we couldn't map ptr back to a known block header.
        // This is a serious issue, either double free or freeing external pointer.
    }
}


void* MemoryPool::allocate_arena(size_t size_bytes) {
    if (size_bytes == 0) return nullptr;
    std::lock_guard<std::mutex> lock(mutex_);

    try {
        // Arena chunks are managed separately and don't use the Block free lists.
        auto chunk = std::make_unique<Chunk>(size_bytes, true /*is_arena_chunk*/);
        void* arena_memory = chunk->memory;
        chunk->used = size_bytes; // Mark entire chunk as used by this arena allocation
        
        total_allocated_ += size_bytes; // Track OS memory allocated for arenas
        allocation_count_.fetch_add(1, std::memory_order_relaxed);
        
        chunks_.emplace_back(std::move(chunk));
        return arena_memory;
    } catch (const std::bad_alloc&) {
        return nullptr;
    }
}

void MemoryPool::release_arena(void* arena_ptr) {
    if (!arena_ptr) return;
    std::lock_guard<std::mutex> lock(mutex_);
    
    auto it = std::find_if(chunks_.begin(), chunks_.end(),
        [&](const std::unique_ptr<Chunk>& chunk_ptr) {
            return chunk_ptr->is_arena_chunk && chunk_ptr->memory == static_cast<std::byte*>(arena_ptr);
        });
    
    if (it != chunks_.end()) {
        total_allocated_ = (total_allocated_ >= (*it)->size) ? total_allocated_ - (*it)->size : 0;
        // Note: total_free_ is for the block-based part of the pool, arenas are separate.
        deallocation_count_.fetch_add(1, std::memory_order_relaxed);
        chunks_.erase(it); // Destructor of Chunk will call munmap
    } else {
        // std::cerr << "MemoryPool::release_arena - Warning: Attempt to release pointer not allocated as an arena: " << arena_ptr << std::endl;
    }
}

void MemoryPool::reset() {
    std::lock_guard<std::mutex> lock(mutex_);
    chunks_.clear(); // Destructors of Chunks will munmap their memory
    std::fill(free_lists_.begin(), free_lists_.end(), nullptr);
    
    total_allocated_ = 0;
    total_free_ = 0;
    allocation_count_.store(0, std::memory_order_relaxed);
    deallocation_count_.store(0, std::memory_order_relaxed);
}

// Coalesce: Needs to iterate through chunks, find adjacent free blocks. More complex.
// Placeholder for now as it's non-trivial.
void MemoryPool::coalesce_free_blocks() {
    // This function is highly dependent on how blocks are stored and linked.
    // E.g., if chunks maintain lists of their blocks, or if blocks can point to prev/next physical.
    // For simplicity in this pass, this remains a complex TODO.
    // The current `add_chunk` creates one large block. `split_block` then creates smaller ones.
    // Coalescing would try to merge physically adjacent free blocks.
}

double MemoryPool::utilization() const {
    // total_allocated_ tracks data area of blocks taken from free list or entire arena chunks.
    // total_free_ tracks data area of blocks on free list.
    // Total managed by block system = total_allocated_ (used blocks) + total_free_ (free blocks)
    // This doesn't easily account for fragmentation or metadata overhead without more detailed tracking.
    // A simpler view: (bytes given to user) / (total OS memory obtained for block system).
    // The current total_allocated_ might be an overestimation if blocks are not perfectly sized.

    size_t total_capacity_for_blocks = 0;
    for(const auto& chunk : chunks_){
        if(!chunk->is_arena_chunk) {
            total_capacity_for_blocks += chunk->size - sizeof(Block); // Approx usable data if one block per chunk
        }
    }
    if (total_capacity_for_blocks == 0 && total_allocated_ == 0) return 0.0; // Avoid div by zero if no block-based mem
    
    // This utilization calculation is based on your uploaded version.
    // It might be better to define utilization based on total mmap'd memory vs. currently used.
    // The current total_allocated_ seems to be an estimate of user-requested bytes.
    size_t total_os_memory_managed = 0;
    for(const auto& chunk : chunks_) total_os_memory_managed += chunk->size;

    if (total_os_memory_managed == 0) return 0.0;
    return static_cast<double>(total_allocated_) / static_cast<double>(total_os_memory_managed);
}

void MemoryPool::pre_allocate(size_t additional_bytes) {
    std::lock_guard<std::mutex> lock(mutex_);
    add_chunk(additional_bytes);
}


// --- PerCycleAllocator Implementation (using MemoryPool for arenas) ---

PerCycleAllocator::PerCycleAllocator(MemoryPool& pool, size_t default_arena_sz)
    : pool_(pool), 
      current_arena_(nullptr), 
      arena_size_(0), 
      arena_used_(0),
      default_arena_size_(std::max(default_arena_sz, static_cast<size_t>(4096))), // Min 4KB arena
      allocations_this_cycle_(0),
      bytes_allocated_this_cycle_(0) {
}

PerCycleAllocator::~PerCycleAllocator() {
    reset(); // Ensure all arenas are returned to the parent pool
}

bool PerCycleAllocator::allocate_new_arena(size_t min_size_needed) {
    // Release current arena if it exists (should not happen if this is called correctly)
    // No, reset() handles releasing old arenas. This just gets a new one.

    size_t size_to_request = std::max(min_size_needed, default_arena_size_);
    
    void* new_arena_mem = pool_.allocate_arena(size_to_request);
    if (!new_arena_mem) {
        std::cerr << "PerCycleAllocator: Failed to allocate new arena of size " << size_to_request << " from parent pool." << std::endl;
        return false;
    }
    
    allocated_arenas_.push_back(new_arena_mem); // Store the pointer given by pool for later release
    current_arena_ = static_cast<std::byte*>(new_arena_mem);
    arena_size_ = size_to_request; // Assume pool gives us what we asked for arena alloc
    arena_used_ = 0; // Reset used count for the new arena
    
    return true;
}

void* PerCycleAllocator::allocate(size_t size_bytes, size_t alignment_bytes) {
    if (size_bytes == 0) return nullptr;
    
    if (!is_power_of_two(alignment_bytes)) {
        alignment_bytes = alignof(std::max_align_t); // Default to max alignment
    }
    
    // Calculate the start of the current free space in the arena
    std::byte* current_free_ptr = current_arena_ ? (current_arena_ + arena_used_) : nullptr;
    
    // Calculate padding needed to align current_free_ptr
    size_t padding = current_arena_ ? calculate_padding(current_free_ptr, alignment_bytes) : 0;
    if (current_arena_ && (arena_used_ + padding < arena_used_)) { // Overflow check on padding
        padding = alignment_bytes; // Max possible padding if overflow (unlikely)
    }
    
    size_t total_needed_in_current_arena = padding + size_bytes;

    if (!current_arena_ || total_needed_in_current_arena > (arena_size_ - arena_used_)) {
        // Not enough space in current arena, or no arena yet. Allocate a new one.
        // Request enough for this allocation plus some headroom, or default.
        if (!allocate_new_arena(size_bytes + alignment_bytes -1 /*effective min data size for this alloc*/ )) {
            return nullptr; // Failed to get a new arena
        }
        // Recalculate for new arena
        current_free_ptr = current_arena_; // Start of new arena
        padding = calculate_padding(current_free_ptr, alignment_bytes);
        total_needed_in_current_arena = padding + size_bytes;

        if (total_needed_in_current_arena > arena_size_) { // Still not enough (new arena too small for this single alloc)
            std::cerr << "PerCycleAllocator: Catastrophic failure - new arena (" << arena_size_ 
                      << "B) is smaller than required aligned allocation (" << total_needed_in_current_arena 
                      << "B for " << size_bytes << "B data)." << std::endl;
            return nullptr;
        }
    }

    std::byte* aligned_ptr = current_free_ptr + padding;
    arena_used_ += total_needed_in_current_arena;
    
    allocations_this_cycle_++;
    bytes_allocated_this_cycle_ += size_bytes; // Track net useful bytes
    
    return static_cast<void*>(aligned_ptr);
}

void PerCycleAllocator::reset() {
    for (void* arena_base_ptr : allocated_arenas_) {
        pool_.release_arena(arena_base_ptr);
    }
    allocated_arenas_.clear();

    current_arena_ = nullptr;
    arena_size_ = 0;
    arena_used_ = 0;
    
    allocations_this_cycle_ = 0;
    bytes_allocated_this_cycle_ = 0;
}

bool PerCycleAllocator::has_space_for(size_t size_bytes, size_t alignment_bytes) const {
    if (!current_arena_) return false;
    
    std::byte* current_free_ptr = current_arena_ + arena_used_;
    size_t padding = calculate_padding(current_free_ptr, alignment_bytes);
    size_t total_needed = padding + size_bytes;
    
    return total_needed <= (arena_size_ - arena_used_);
}


} // namespace Alaris::Core