#pragma once

#include <functional>
#include <vector>
#include <chrono>
#include <atomic>
#include <thread> 
#include <string> 

namespace Alaris::Core {

class TimeTriggeredExecutor {
public:
    using TaskFunction = std::function<void()>;
    using Clock = std::chrono::high_resolution_clock;
    using TimePoint = Clock::time_point;
    using Duration = Clock::duration;

private:
    struct Task {
        std::string name;
        TaskFunction function;
        Duration period;
        Duration phase_offset;
        Duration deadline;
        TimePoint last_execution_scheduled_time;
        TimePoint last_execution_actual_start_time;
        
        uint64_t execution_count;
        uint64_t missed_deadlines_count;
        Duration total_execution_time_ns;
        Duration max_execution_time_ns;

        // Constructor declaration only - definition in .cpp
        Task(std::string task_name, TaskFunction func, Duration per, Duration offset, Duration dead);
    };

    std::vector<Task> tasks_;
    Duration major_frame_;
    std::atomic<bool> stop_requested_;
    uint64_t current_major_frame_count_ = 0;
    TimePoint executor_start_time_;

    // Performance Monitoring
    struct CyclePerformanceMetrics {
        TimePoint actual_start_time;
        Duration actual_duration_ns;
        Duration intended_duration_ns;
        Duration jitter_ns;
        size_t tasks_executed_in_cycle;
        size_t deadlines_missed_in_cycle;
    };

    std::vector<CyclePerformanceMetrics> cycle_history_;
    static constexpr size_t MAX_CYCLE_HISTORY = 1000;

    // Private helper methods
    void execute_tasks_for_current_frame(TimePoint current_frame_ideal_start_time);
    void record_cycle_metrics(const CyclePerformanceMetrics& metrics);
    bool should_execute_task(const Task& task, TimePoint cycle_start) const;
    void execute_task(Task& task, TimePoint cycle_start);

public:
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
     * @brief Simplified task registration for common use cases
     * @param task The function to execute
     * @param period The period at which the task should run
     */
    void register_task(TaskFunction task, Duration period);

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
    bool is_running() const;

    // Performance Metrics Structure - Fixed to include all expected fields
    struct PerformanceReport {
        // Core metrics
        double average_major_frame_time_us = 0.0;
        double max_major_frame_time_us = 0.0;
        double min_major_frame_time_us = 0.0;
        double average_jitter_us = 0.0;
        double max_jitter_us = 0.0;
        uint64_t total_major_frames_executed = 0;
        uint64_t total_task_deadlines_missed_overall = 0;

        // Backward compatibility fields
        uint64_t total_cycles = 0;
        uint64_t total_deadlines_missed = 0;
        double average_cycle_time_us = 0.0;
        double max_cycle_time_us = 0.0;

        struct TaskReport {
            std::string name;
            uint64_t execution_count = 0;
            uint64_t missed_deadlines_count = 0;
            double average_execution_time_us = 0.0;
            double max_execution_time_us = 0.0;
            double miss_rate_percent = 0.0;
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