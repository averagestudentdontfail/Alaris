// test/ipc/shared_memory_test.cpp
#include "gtest/gtest.h"
#include "src/quantlib/ipc/shared_ring_buffer.h" // Adjusted path
#include "src/quantlib/ipc/message_types.h"    // Adjusted path
#include <thread>
#include <vector>
#include <cstring> // For memcmp

// Define a simple struct for testing
struct TestMessage {
    int id;
    double value;
    char data[16];

    bool operator==(const TestMessage& other) const {
        return id == other.id && std::abs(value - other.value) < 1e-9 && std::memcmp(data, other.data, sizeof(data)) == 0;
    }
};
static_assert(std::is_trivially_copyable_v<TestMessage>, "TestMessage must be trivially copyable");


const char* test_shm_name = "/alaris_test_shm_ring_buffer";

// Fixture for SharedRingBuffer tests
class SharedRingBufferTest : public ::testing::Test {
protected:
    void SetUp() override {
        // Ensure shared memory is unlinked before each test, in case previous test crashed
        shm_unlink(test_shm_name);
    }

    void TearDown() override {
        // Clean up shared memory after each test
        shm_unlink(test_shm_name);
    }
};

TEST_F(SharedRingBufferTest, CreateAndDestroy) {
    ASSERT_NO_THROW({
        Alaris::IPC::SharedRingBuffer<TestMessage, 256> buffer(test_shm_name, true);
    });
    // Destructor will be called, which should unlink.
}

TEST_F(SharedRingBufferTest, OpenExisting) {
    {
        Alaris::IPC::SharedRingBuffer<TestMessage, 256> producer(test_shm_name, true);
        // Producer creates and owns the SHM segment
    } // Producer goes out of scope, but SHM segment should remain if not unlinked by owner immediately (which it is in destructor)
      // For this test to work as "open existing", the first instance should not unlink, or a more complex setup is needed.
      // The current destructor unlinks if is_owner_. Let's test with is_owner_ = false for consumer.

    // Re-create the producer, which also handles ftruncate
    Alaris::IPC::SharedRingBuffer<TestMessage, 256> producer_again(test_shm_name, true);

    ASSERT_NO_THROW({
        Alaris::IPC::SharedRingBuffer<TestMessage, 256> consumer(test_shm_name, false);
    }); // Consumer opens existing, does not own, should not unlink.
}


TEST_F(SharedRingBufferTest, SingleWriteRead) {
    Alaris::IPC::SharedRingBuffer<TestMessage, 256> buffer(test_shm_name, true);
    TestMessage write_msg = {1, 3.14, "hello"};
    TestMessage read_msg = {};

    ASSERT_TRUE(buffer.try_write(write_msg));
    ASSERT_EQ(buffer.size(), 1);
    ASSERT_TRUE(buffer.try_read(read_msg));
    ASSERT_EQ(buffer.size(), 0);

    EXPECT_EQ(read_msg.id, write_msg.id);
    EXPECT_DOUBLE_EQ(read_msg.value, write_msg.value);
    EXPECT_STREQ(read_msg.data, write_msg.data);
}

TEST_F(SharedRingBufferTest, BufferFull) {
    const size_t buffer_capacity = 16;
    Alaris::IPC::SharedRingBuffer<TestMessage, buffer_capacity> buffer(test_shm_name, true);

    for (size_t i = 0; i < buffer_capacity; ++i) {
        ASSERT_TRUE(buffer.try_write({static_cast<int>(i), static_cast<double>(i), ""}));
    }
    ASSERT_EQ(buffer.size(), buffer_capacity);
    ASSERT_TRUE(buffer.full());

    TestMessage extra_msg = {999, 0.0, ""};
    EXPECT_FALSE(buffer.try_write(extra_msg)) << "Write should fail when buffer is full.";
}

TEST_F(SharedRingBufferTest, BufferEmpty) {
    Alaris::IPC::SharedRingBuffer<TestMessage, 16> buffer(test_shm_name, true);
    TestMessage msg;

    ASSERT_TRUE(buffer.empty());
    ASSERT_EQ(buffer.size(), 0);
    EXPECT_FALSE(buffer.try_read(msg)) << "Read should fail when buffer is empty.";
}

TEST_F(SharedRingBufferTest, MultipleProducersSingleConsumer) {
    Alaris::IPC::SharedRingBuffer<TestMessage, 1024> buffer(test_shm_name, true); // Main creator

    std::atomic<int> messages_written{0};
    std::atomic<int> messages_read{0};
    const int msgs_per_producer = 200;

    auto producer_func = [&](int start_id) {
        // Each producer thread needs its own SharedRingBuffer object to access the shared memory
        Alaris::IPC::SharedRingBuffer<TestMessage, 1024> local_producer_buffer(test_shm_name, false); // Open existing
        for (int i = 0; i < msgs_per_producer; ++i) {
            TestMessage msg = {start_id + i, static_cast<double>(start_id + i), "prod"};
            while (!local_producer_buffer.try_write(msg)) {
                std::this_thread::yield(); // Spin if full
            }
            messages_written++;
        }
    };

    std::thread p1(producer_func, 1000);
    std::thread p2(producer_func, 2000);

    p1.join();
    p2.join();

    EXPECT_EQ(messages_written.load(), 2 * msgs_per_producer);
    EXPECT_EQ(buffer.size(), 2 * msgs_per_producer);

    TestMessage read_msg;
    for (int i = 0; i < 2 * msgs_per_producer; ++i) {
        ASSERT_TRUE(buffer.try_read(read_msg));
        messages_read++;
    }
    EXPECT_EQ(messages_read.load(), 2 * msgs_per_producer);
    EXPECT_TRUE(buffer.empty());
}

TEST_F(SharedRingBufferTest, BatchWriteRead) {
    Alaris::IPC::SharedRingBuffer<TestMessage, 256> buffer(test_shm_name, true);
    std::vector<TestMessage> write_batch(10);
    for(int i=0; i<10; ++i) write_batch[i] = {i, i*1.1, "batch"};

    size_t written = buffer.try_write_batch(write_batch.data(), write_batch.size());
    ASSERT_EQ(written, write_batch.size());
    ASSERT_EQ(buffer.size(), write_batch.size());

    std::vector<TestMessage> read_batch(10);
    size_t read_count = buffer.try_read_batch(read_batch.data(), read_batch.size());
    ASSERT_EQ(read_count, write_batch.size());
    ASSERT_TRUE(buffer.empty());

    for(size_t i=0; i<write_batch.size(); ++i) {
        EXPECT_EQ(read_batch[i], write_batch[i]);
    }
}

TEST_F(SharedRingBufferTest, MoveConstructor) {
    Alaris::IPC::SharedRingBuffer<TestMessage, 16> buffer1(test_shm_name, true);
    TestMessage msg1 = {1, 1.0, "msg1"};
    ASSERT_TRUE(buffer1.try_write(msg1));

    Alaris::IPC::SharedRingBuffer<TestMessage, 16> buffer2(std::move(buffer1));
    // buffer1 should be in a valid but unusable state (no longer owns/manages SHM)
    
    ASSERT_FALSE(buffer1.try_write({2,2.0,""}) ); // Operations on moved-from buffer should ideally fail or be no-ops
    ASSERT_EQ(buffer2.size(), 1);

    TestMessage read_msg;
    ASSERT_TRUE(buffer2.try_read(read_msg));
    EXPECT_EQ(read_msg.id, msg1.id);
}

// Note: IPC tests involving actual cross-process communication are harder to unit test
// and usually fall into integration testing. These tests focus on the buffer logic itself.