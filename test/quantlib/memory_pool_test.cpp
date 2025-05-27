// test/core/memory_pool_test.cpp
#include "gtest/gtest.h"
#include "src/quantlib/core/memory_pool.h" // Adjusted path

// Test fixture for MemoryPool tests
class MemoryPoolTest : public ::testing::Test {
protected:
    std::unique_ptr<Alaris::Core::MemoryPool> pool_;
    const size_t initial_pool_size_ = 1 * 1024 * 1024; // 1MB

    void SetUp() override {
        pool_ = std::make_unique<Alaris::Core::MemoryPool>(initial_pool_size_);
    }
};

TEST_F(MemoryPoolTest, Initialization) {
    ASSERT_NE(pool_, nullptr);
    // Initial state: total_allocated should be 0, total_free might reflect the initial chunk
    // The current `add_chunk` adds one block to free list, and `total_free_` tracks its data size.
    // `total_allocated_` is 0.
    EXPECT_EQ(pool_->total_allocated(), 0);
    EXPECT_GT(pool_->total_free(), 0); // Should have some free space from initial chunk
    EXPECT_EQ(pool_->allocation_count(), 0);
    EXPECT_EQ(pool_->deallocation_count(), 0);
}

TEST_F(MemoryPoolTest, SimpleAllocation) {
    void* ptr = pool_->allocate(128, 64);
    ASSERT_NE(ptr, nullptr);
    EXPECT_GT(pool_->total_allocated(), 0); // Should reflect the block size, not just 128
    EXPECT_EQ(pool_->allocation_count(), 1);

    // Try to write to allocated memory
    int* int_ptr = static_cast<int*>(ptr);
    ASSERT_NO_THROW(*int_ptr = 12345);
    EXPECT_EQ(*int_ptr, 12345);

    pool_->release(ptr);
    EXPECT_EQ(pool_->deallocation_count(), 1);
    // Total allocated might not go to zero if block based, but total_free should increase.
}

TEST_F(MemoryPoolTest, MultipleAllocationsAndDeallocations) {
    const int num_allocs = 100;
    std::vector<void*> ptrs;
    ptrs.reserve(num_allocs);

    for (int i = 0; i < num_allocs; ++i) {
        void* p = pool_->allocate(64 + i * 8, 64); // Varying sizes
        ASSERT_NE(p, nullptr) << "Allocation " << i << " failed.";
        ptrs.push_back(p);
        // Simple write test
        char* char_p = static_cast<char*>(p);
        *char_p = static_cast<char>('a' + (i % 26)); 
    }
    EXPECT_EQ(pool_->allocation_count(), num_allocs);

    for (int i = 0; i < num_allocs; ++i) {
        char* char_p = static_cast<char*>(ptrs[i]);
        EXPECT_EQ(*char_p, static_cast<char>('a' + (i % 26)));
        pool_->release(ptrs[i]);
    }
    EXPECT_EQ(pool_->deallocation_count(), num_allocs);
    // After all releases, total_allocated should be close to 0 (or reflect overhead of used blocks before they are fully free)
    // and total_free should be close to initial. This depends heavily on coalesce implementation.
}


TEST_F(MemoryPoolTest, ArenaAllocation) {
    size_t arena_size = 256 * 1024; // 256KB
    void* arena_ptr = pool_->allocate_arena(arena_size);
    ASSERT_NE(arena_ptr, nullptr);
    
    // Try to use the arena memory
    int* data = static_cast<int*>(arena_ptr);
    size_t num_ints = arena_size / sizeof(int);
    for (size_t i = 0; i < num_ints; ++i) {
        data[i] = static_cast<int>(i);
    }
    EXPECT_EQ(data[num_ints - 1], static_cast<int>(num_ints - 1));

    pool_->release_arena(arena_ptr);
}

TEST_F(MemoryPoolTest, PerCycleAllocatorBasic) {
    Alaris::Core::PerCycleAllocator cycle_allocator(*pool_);
    
    void* p1 = cycle_allocator.allocate(100, 64);
    ASSERT_NE(p1, nullptr);
    int* int_p1 = static_cast<int*>(p1);
    *int_p1 = 10;

    void* p2 = cycle_allocator.allocate(200, 64);
    ASSERT_NE(p2, nullptr);
    int* int_p2 = static_cast<int*>(p2);
    *int_p2 = 20;

    EXPECT_EQ(*int_p1, 10);
    EXPECT_EQ(*int_p2, 20);
    EXPECT_EQ(cycle_allocator.get_allocation_count_this_cycle(), 2);
    EXPECT_EQ(cycle_allocator.get_bytes_allocated_this_cycle(), 100 + 200);

    cycle_allocator.reset(); // Returns arenas to the main pool

    EXPECT_EQ(cycle_allocator.get_allocation_count_this_cycle(), 0);
    EXPECT_EQ(cycle_allocator.get_bytes_allocated_this_cycle(), 0);

    // Try allocating again after reset
    void* p3 = cycle_allocator.allocate(50, 64);
    ASSERT_NE(p3, nullptr);
}

TEST_F(MemoryPoolTest, AlignmentTest) {
    const size_t alignment = 128;
    void* ptr = pool_->allocate(256, alignment);
    ASSERT_NE(ptr, nullptr);
    EXPECT_EQ(reinterpret_cast<uintptr_t>(ptr) % alignment, 0) 
        << "Pointer " << ptr << " not aligned to " << alignment;
    pool_->release(ptr);

    void* ptr2 = pool_->allocate(77, 32); // Odd size, common alignment
    ASSERT_NE(ptr2, nullptr);
    EXPECT_EQ(reinterpret_cast<uintptr_t>(ptr2) % 32, 0)
        << "Pointer " << ptr2 << " not aligned to 32";
    pool_->release(ptr2);
}


TEST_F(MemoryPoolTest, LargeAllocation) {
    // Try to allocate something larger than default_chunk_size if pool is small,
    // or a significant portion of the pool.
    // MemoryPool's add_chunk logic should handle this.
    // default_chunk_size_ is 16MB in memory_pool.cpp
    size_t large_alloc_size = initial_pool_size_ / 2; 
    if (large_alloc_size == 0) large_alloc_size = 1024*1024; // Ensure positive for tiny initial pools
    
    void* ptr = pool_->allocate(large_alloc_size, 64);
    ASSERT_NE(ptr, nullptr);
    
    // Simple write test
    memset(ptr, 0xAB, large_alloc_size);
    EXPECT_EQ(static_cast<unsigned char*>(ptr)[0], 0xAB);
    EXPECT_EQ(static_cast<unsigned char*>(ptr)[large_alloc_size-1], 0xAB);

    pool_->release(ptr);
}

TEST_F(MemoryPoolTest, PoolReset) {
    void* p1 = pool_->allocate(100, 64);
    void* arena1 = pool_->allocate_arena(10000);
    ASSERT_NE(p1, nullptr);
    ASSERT_NE(arena1, nullptr);
    EXPECT_GT(pool_->total_allocated(), 0);

    pool_->reset();

    EXPECT_EQ(pool_->total_allocated(), 0);
    EXPECT_EQ(pool_->total_free(), 0); // After reset, no chunks, so free is 0
    EXPECT_EQ(pool_->allocation_count(), 0);
    EXPECT_EQ(pool_->deallocation_count(), 0);

    // Try allocating again after reset
    void* p2 = pool_->allocate(50, 64); // This should trigger new chunk allocation
    ASSERT_NE(p2, nullptr);
    EXPECT_GT(pool_->total_allocated(), 0);
    pool_->release(p2);
}