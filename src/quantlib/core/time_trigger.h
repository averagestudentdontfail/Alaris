// src/quantlib/core/time_trigger.h
#pragma once

#include <functional>
#include <vector>
#include <chrono>
#include <atomic>
#include <thread>

namespace Alaris::Core {

class TimeTriggeredExecutor {
public:
    using TaskFunction = std::function<void()>;
    using Clock = std::chrono::high_resolution_clock;
    using TimePoint = Clock::time_point;
    using Duration = Clock::duration;
    
private:
    struct Task {
        TaskFunction function;
        Duration period;
        Duration phase_offset;
        Duration deadline;
        TimePoint last_execution;
        uint64_t execution_count;
        uint64_t missed_deadlines;
        Duration total_execution_time;
        Duration max_execution_time;
        
        Task(TaskFunction func, Duration per, Duration offset, Duration dead)
            : function(std::move(func)), period(per), phase_offset(offset),
              deadline(dead), last_execution(TimePoint::min()),
              execution_count(0), missed_deadlines(0),
              total_execution_time(Duration::zero()),
              max_execution_time(Duration::zero()) {}
    };
    
    std::vector<Task> tasks_;
    Duration major_frame_;
    std::atomic<bool> running_;
    std::atomic<bool> stop_requested_;
    TimePoint start_time_;
    
    // Performance monitoring
    struct CycleMetrics {
        Duration cycle_time;
        Duration jitter;
        size_t tasks_executed;
        size_t deadlines_missed;
    };
    
    std::vector<CycleMetrics> cycle_history_;
    static constexpr size_t MAX_HISTORY = 10000;
    
    void execute_cycle();
    void collect_metrics(const CycleMetrics& metrics);
    
public:
    explicit TimeTriggeredExecutor(Duration major_frame);
    ~TimeTriggeredExecutor();
    
    // Non-copyable
    TimeTriggeredExecutor(const TimeTriggeredExecutor&) = delete;
    TimeTriggeredExecutor& operator=(const TimeTriggeredExecutor&) = delete;
    
    // Register a task for periodic execution
    void register_task(TaskFunction task, Duration period, 
                      Duration phase_offset = Duration::zero(),
                      Duration deadline = Duration::zero());
    
    // Execute one cycle of the schedule
    void execute_single_cycle();
    
    // Run executor for specified duration
    void run(Duration duration);
    
    // Run executor until stop is requested
    void run_continuous();
    
    // Request stop
    void stop();
    
    // Performance metrics
    struct PerformanceMetrics {
        double average_cycle_time_us;
        double max_cycle_time_us;
        double average_jitter_us;
        double max_jitter_us;
        size_t total_cycles;
        size_t total_deadlines_missed;
        double deadline_miss_rate;
        
        struct TaskMetrics {
            size_t task_id;
            double average_execution_time_us;
            double max_execution_time_us;
            size_t execution_count;
            size_t missed_deadlines;
            double miss_rate;
        };
        
        std::vector<TaskMetrics> task_metrics;
    };
    
    PerformanceMetrics get_performance_metrics() const;
    void reset_metrics();
    
    // System status
    bool is_running() const { return running_.load(); }
    size_t task_count() const { return tasks_.size(); }
    Duration get_major_frame() const { return major_frame_; }
};

} // namespace Alaris::Core