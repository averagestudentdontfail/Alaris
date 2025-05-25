#pragma once

#include <functional>
#include <vector>
#include <chrono>
#include <atomic>
#include <thread> // For std::this_thread::sleep_for, not for creating threads here
#include <string> // For task names in metrics

namespace Alaris::Core {

class TimeTriggeredExecutor {
public:
    using TaskFunction = std::function<void()>;
    using Clock = std::chrono::high_resolution_clock;
    using TimePoint = Clock::time_point;
    using Duration = Clock::duration;

private:
    struct Task {
        std::string name; // Optional: for identifiable metrics
        TaskFunction function;
        Duration period;
        Duration phase_offset; // Relative to the start of the executor's run
        Duration deadline;     // Relative to the task's scheduled start time
        TimePoint last_execution_scheduled_time; // When it was last scheduled to run
        TimePoint last_execution_actual_start_time; // When it actually started
        
        uint64_t execution_count;
        uint64_t missed_deadlines_count;
        Duration total_execution_time_ns; // Sum of actual execution durations
        Duration max_execution_time_ns;   // Longest actual execution duration

        Task(std::string task_name, TaskFunction func, Duration per, Duration offset, Duration dead)
            : name(std::move(task_name)),
              function(std::move(func)),
              period(per),
              phase_offset(offset),
              deadline(dead),
              last_execution_scheduled_time(TimePoint::min()),
              last_execution_actual_start_time(TimePoint::min()),
              execution_count(0),
              missed_deadlines_count(0),
              total_execution_time_ns(Duration::zero()),
              max_execution_time_ns(Duration::zero()) {}
    };

    std::vector<Task> tasks_;
    Duration major_frame_;              // The fundamental period of the executor
    std::atomic<bool> stop_requested_;  // For explicit stop calls
    TimePoint executor_start_time_;     // Time when the run/run_continuous started
    uint64_t current_major_frame_count_ = 0;

    // Performance Monitoring
    struct CyclePerformanceMetrics {
        TimePoint actual_start_time;
        Duration actual_duration_ns;    // How long this major frame actually took
        Duration intended_duration_ns;  // Should be major_frame_
        Duration jitter_ns;             // Difference between actual and intended start relative to previous frame end
        size_t tasks_executed_in_cycle;
        size_t deadlines_missed_in_cycle;
    };

    std::vector<CyclePerformanceMetrics> cycle_history_; // Stores metrics for recent cycles
    static constexpr size_t MAX_CYCLE_HISTORY = 1000;   // Max history to keep for metrics

    void execute_tasks_for_current_frame(TimePoint current_frame_ideal_start_time);
    void record_cycle_metrics(const CyclePerformanceMetrics& metrics);

public:
    /**
     * @brief Constructs a TimeTriggeredExecutor.
     * @param major_frame The fundamental execution cycle period.
     */
    explicit TimeTriggeredExecutor(Duration major_frame);
    ~TimeTriggeredExecutor();

    // Non-copyable
    TimeTriggeredExecutor(const TimeTriggeredExecutor&) = delete;
    TimeTriggeredExecutor& operator=(const TimeTriggeredExecutor&) = delete;

    /**
     * @brief Registers a task for periodic execution.
     * @param task_name A descriptive name for the task (for metrics).
     * @param task The function to execute.
     * @param period The period at which the task should run. Must be a multiple of major_frame.
     * @param phase_offset The offset from the start of the executor's run time. Must be a multiple of major_frame.
     * @param deadline The deadline for task completion, relative to its scheduled start time. Defaults to its period.
     */
    void register_task(std::string task_name,
                       TaskFunction task,
                       Duration period,
                       Duration phase_offset = Duration::zero(),
                       Duration deadline = Duration::zero());

    /**
     * @brief Runs the executor for a specified duration.
     * @param duration Total duration to run the executor.
     * @param shutdown_flag An atomic flag to signal external shutdown requests.
     */
    void run(Duration duration, std::atomic<bool>& shutdown_flag);

    /**
     * @brief Runs the executor continuously until an external shutdown is signaled.
     * @param shutdown_flag An atomic flag. The executor will stop when this is true or its internal stop() is called.
     */
    void run_continuous(std::atomic<bool>& shutdown_flag);

    /**
     * @brief Requests the executor to stop its execution loop.
     */
    void stop();

    /**
     * @brief Checks if the executor is currently running.
     * @return True if running, false otherwise.
     */
    bool is_running() const; // Removed internal running_ flag, use executor_thread state or similar if threaded

    // Performance Metrics Structure
    struct PerformanceReport {
        double average_major_frame_time_us;
        double max_major_frame_time_us;
        double min_major_frame_time_us;
        double average_jitter_us; // Jitter in major frame start times
        double max_jitter_us;
        uint64_t total_major_frames_executed;
        uint64_t total_task_deadlines_missed_overall;

        struct TaskReport {
            std::string name;
            uint64_t execution_count;
            uint64_t missed_deadlines_count;
            double average_execution_time_us;
            double max_execution_time_us;
            double miss_rate_percent; // (missed_deadlines_count / execution_count) * 100
        };
        std::vector<TaskReport> task_reports;
    };

    /**
     * @brief Retrieves the performance metrics collected by the executor.
     * @return A PerformanceReport struct.
     */
    PerformanceReport get_performance_metrics() const;

    /**
     * @brief Resets all collected performance metrics.
     */
    void reset_metrics();

    /**
     * @brief Gets the configured major frame duration.
     * @return The major frame duration.
     */
    Duration get_major_frame() const { return major_frame_; }
    
    /**
     * @brief Gets the number of tasks registered with the executor.
     * @return Count of tasks.
     */
    size_t task_count() const { return tasks_.size(); }
};

} // namespace Alaris::Core